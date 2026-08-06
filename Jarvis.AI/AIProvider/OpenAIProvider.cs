using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Jarvis.AI.Configuration;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.AIProvider;

/// <summary>
/// Cloud AI provider speaking the OpenAI-compatible chat completions protocol. Because the
/// protocol is a de-facto standard, this single provider works with Groq, OpenAI, Together,
/// OpenRouter, Google Gemini, DeepSeek, LocalAI and many other backends - all it needs is a
/// base URL and an API key. Each <see cref="OpenAICompatibleOptions"/> entry becomes its own
/// provider instance.
/// </summary>
public sealed class OpenAIProvider : IAIProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAICompatibleOptions _options;
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(
        IHttpClientFactory httpClientFactory,
        OpenAICompatibleOptions options,
        ILogger<OpenAIProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Id => _options.Id;

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName;

    /// <inheritdoc />
    public bool IsLocal => false;

    private string? ApiKey => !string.IsNullOrWhiteSpace(_options.ApiKey)
        ? _options.ApiKey
        : Environment.GetEnvironmentVariable(_options.EnvironmentVariable);

    /// <inheritdoc />
    public async Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        string? apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ProviderHealth.Fail(
                $"No API key configured. Set '{_options.EnvironmentVariable}' or the AI:{_options.Id}:ApiKey setting.");
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("openai");
            using HttpRequestMessage request = new(HttpMethod.Get, $"{_options.BaseUrl}/models");
            request.Headers.Authorization = new("Bearer", apiKey);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProviderHealth.Fail($"Provider responded {(int)response.StatusCode}.");
            }

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var models = new List<string>();
            if (document.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement model in data.EnumerateArray())
                {
                    if (model.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String)
                    {
                        models.Add(id.GetString() ?? string.Empty);
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
        HttpClient client = _httpClientFactory.CreateClient("openai");
        string payload = JsonSerializer.Serialize(new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = temperature ?? 0.7d,
            max_tokens = maxTokens,
            stream = false,
        });

        using HttpRequestMessage request = new(HttpMethod.Post, $"{_options.BaseUrl}/chat/completions");
        request.Headers.Authorization = new("Bearer", ApiKey);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        long startedAt = Environment.TickCount64;
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = document.RootElement;

        string content = string.Empty;
        if (root.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out JsonElement message)
                    && message.TryGetProperty("content", out JsonElement contentElement)
                    && contentElement.ValueKind == JsonValueKind.String)
                {
                    content = contentElement.GetString() ?? string.Empty;
                    break;
                }
            }
        }

        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out JsonElement usage))
        {
            promptTokens = GetIntOrNull(usage, "prompt_tokens");
            completionTokens = GetIntOrNull(usage, "completion_tokens");
        }

        string? finishReason = null;
        if (root.TryGetProperty("choices", out choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            finishReason = GetStringOrNull(choices[0], "finish_reason");
        }

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
        HttpClient client = _httpClientFactory.CreateClient("openai");
        string payload = JsonSerializer.Serialize(new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = temperature ?? 0.7d,
            max_tokens = maxTokens,
            stream = true,
        });

        using HttpRequestMessage request = new(HttpMethod.Post, $"{_options.BaseUrl}/chat/completions");
        request.Headers.Authorization = new("Bearer", ApiKey);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            const string dataPrefix = "data: ";
            if (!line.StartsWith(dataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string data = line[dataPrefix.Length..].Trim();
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                yield return new ChatChunk { Delta = string.Empty, Model = model, Done = true };
                yield break;
            }

            if (data.Length == 0)
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(data);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("delta", out JsonElement delta)
                        && delta.TryGetProperty("content", out JsonElement content)
                        && content.ValueKind == JsonValueKind.String)
                    {
                        yield return new ChatChunk { Delta = content.GetString() ?? string.Empty, Model = model };
                    }
                }
            }
        }
    }

    private static int? GetIntOrNull(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : null;

    private static string? GetStringOrNull(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
