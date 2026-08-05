using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Jarvis.AI.Configuration;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.AIProvider;

/// <summary>
/// Local AI provider backed by an Ollama instance running on this machine. Works fully offline
/// and needs no API key.
/// </summary>
public sealed class OllamaProvider : IAIProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaProvider> _logger;

    public OllamaProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AIOptions> options,
        ILogger<OllamaProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Ollama;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Id => "ollama";

    /// <inheritdoc />
    public string DisplayName => "Ollama (local)";

    /// <inheritdoc />
    public bool IsLocal => true;

    /// <inheritdoc />
    public async Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("ollama");
            using HttpResponseMessage response = await client.GetAsync($"{_options.Endpoint}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProviderHealth.Fail($"Ollama responded {(int)response.StatusCode}.");
            }

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var models = new List<string>();
            if (document.RootElement.TryGetProperty("models", out JsonElement modelsElement) && modelsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement model in modelsElement.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                    {
                        models.Add(name.GetString() ?? string.Empty);
                    }
                }
            }

            return ProviderHealth.Ok(models);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ProviderHealth.Fail(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        ProviderHealth health = await CheckHealthAsync(cancellationToken);
        return health.Models;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double? temperature,
        int? maxTokens,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = _httpClientFactory.CreateClient("ollama");
        var payload = BuildPayload(messages, model, temperature, maxTokens, stream: false);
        using StringContent body = new(payload, Encoding.UTF8, "application/json");

        long startedAt = Environment.TickCount64;
        using HttpResponseMessage response = await client.PostAsync($"{_options.Endpoint}/api/chat", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = document.RootElement;

        string content = string.Empty;
        if (root.TryGetProperty("message", out JsonElement message) && message.TryGetProperty("content", out JsonElement contentElement))
        {
            content = contentElement.GetString() ?? string.Empty;
        }

        int? promptTokens = GetIntOrNull(root, "prompt_eval_count");
        int? completionTokens = GetIntOrNull(root, "eval_count");
        string? finishReason = root.TryGetProperty("done_reason", out JsonElement reason) && reason.ValueKind == JsonValueKind.String
            ? reason.GetString()
            : null;

        return new ChatResponse
        {
            Message = ChatMessage.Assistant(content),
            Model = model,
            Provider = Id,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            FinishReason = finishReason,
            DurationMs = Environment.TickCount64 - startedAt,
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double? temperature,
        int? maxTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        HttpClient client = _httpClientFactory.CreateClient("ollama");
        string payload = BuildPayload(messages, model, temperature, maxTokens, stream: true);
        using StringContent body = new(payload, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync($"{_options.Endpoint}/api/chat", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("message", out JsonElement message)
                && message.TryGetProperty("content", out JsonElement content)
                && content.ValueKind == JsonValueKind.String)
            {
                string delta = content.GetString() ?? string.Empty;
                bool done = root.TryGetProperty("done", out JsonElement doneElement) && doneElement.GetBoolean();
                yield return new ChatChunk { Delta = delta, Model = model, Done = done };
            }
        }
    }

    private string BuildPayload(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double? temperature,
        int? maxTokens,
        bool stream)
    {
        return JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(model) ? _options.Model : model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream,
            options = new { temperature = temperature ?? 0.7d },
            max_tokens = maxTokens,
        });
    }

    private static int? GetIntOrNull(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : null;
}
