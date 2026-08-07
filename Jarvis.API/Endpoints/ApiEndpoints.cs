using System.Text;
using System.Text.Json;
using Jarvis.AI.Configuration;
using Jarvis.API.Contracts;
using Jarvis.Core.Plugins;
using Jarvis.Memory.Configuration;
using Jarvis.SDK.AI;
using Jarvis.SDK.Host;
using Jarvis.SDK.Memory;
using Microsoft.Extensions.Options;

namespace Jarvis.API.Endpoints;

/// <summary>
/// Maps the JARVIS web API: system status, plugin commands, memory, preferences and the AI
/// chat endpoints (JSON and SSE streaming).
/// </summary>
public static class ApiEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static void MapJarvisApi(this WebApplication app)
    {
        RouteGroupBuilder api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        MapSystemEndpoints(api);
        MapPluginEndpoints(api);
        MapMemoryEndpoints(api);
        MapAIEndpoints(api);
    }

    private static void MapSystemEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/status", async (IJarvisHost host, IMemoryService memory, IAIService ai) =>
        {
            var plugins = host.Plugins;
            IReadOnlyList<AIProviderInfo> providers = await ai.GetProvidersAsync();
            return Results.Ok(new
            {
                version = host.ApplicationVersion,
                instanceName = host.InstanceName,
                uptimeSeconds = Environment.TickCount64 / 1000,
                plugins = new { loaded = plugins.Count },
                memory = new { enabled = true, recent = (await memory.GetRecentAsync(3)).Count },
                ai = new
                {
                    providers = providers.Select(provider => new
                    {
                        provider.Id,
                        provider.DisplayName,
                        provider.IsLocal,
                        provider.IsAvailable,
                    }),
                },
            });
        });
    }

    private static void MapPluginEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/plugins", (IJarvisHost host) =>
            Results.Ok(host.Plugins.Select(plugin => new
            {
                id = plugin.Manifest.Id,
                name = plugin.Manifest.Name,
                version = plugin.Manifest.Version,
                description = plugin.Manifest.Description,
                state = plugin.State.ToString(),
                permissions = plugin.Manifest.Permissions,
                commands = plugin.Commands.Select(command => new { command.Name, command.Description }),
            })));

        api.MapPost("/plugins/{pluginId}/commands/{command}", async (
            string pluginId,
            string command,
            PluginCommandRequest request,
            IJarvisHost host) =>
        {
            try
            {
                object? result = await host.ExecuteCommandAsync(pluginId, command, NormalizeParameters(request.Parameters));
                bool isPlainText = result is null || result is string;
                return Results.Ok(new
                {
                    success = true,
                    result = ToResultText(result),
                    // Structured payload for richer clients; null when the result is plain text.
                    data = isPlainText ? null : result,
                });
            }
            catch (Exception exception)
            {
                return Results.Json(
                    new { success = false, error = exception.Message },
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    /// <summary>
    /// Converts a command result into a readable text summary. Enumerables are joined line by
    /// line so list-producing commands render as text for simple clients.
    /// </summary>
    private static string ToResultText(object? result)
    {
        if (result is null)
        {
            return string.Empty;
        }

        if (result is string text)
        {
            return text;
        }

        if (result is System.Collections.IEnumerable sequence)
        {
            var lines = new List<string>();
            foreach (object? item in sequence)
            {
                lines.Add(item?.ToString() ?? string.Empty);
            }

            return lines.Count == 0 ? "(no results)" : string.Join('\n', lines);
        }

        return result.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Converts JSON-element parameter values into plain .NET primitives so plugins can
    /// safely cast with <c>as string</c>/<c>as int?</c> instead of receiving <see cref="JsonElement"/>.
    /// </summary>
    private static Dictionary<string, object?>? NormalizeParameters(Dictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(parameters.Count);
        foreach ((string key, object? value) in parameters)
        {
            result[key] = value is JsonElement element
                ? element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when element.TryGetInt64(out long whole) => whole,
                    JsonValueKind.Number => element.GetDouble(),
                    JsonValueKind.Null => null,
                    JsonValueKind.Array => element.EnumerateArray().Select(item => item.ToString()).ToArray(),
                    JsonValueKind.Object => element.ToString(),
                    _ => element.ToString(),
                }
                : value;
        }

        return result;
    }

    private static void MapMemoryEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/memory/recent", async (int? limit, IMemoryService memory) =>
            Results.Ok(await memory.GetRecentAsync(Math.Clamp(limit ?? 20, 1, 100))));

        api.MapGet("/memory/search", async (string q, string? kind, int? limit, IMemoryService memory) =>
        {
            MemoryKind? parsedKind = Enum.TryParse<MemoryKind>(kind, ignoreCase: true, out MemoryKind value) ? value : null;
            IReadOnlyList<MemorySearchResult> results = await memory.SearchAsync(q, Math.Clamp(limit ?? 10, 1, 50), parsedKind);
            return Results.Ok(results.Select(result => new
            {
                entry = result.Entry,
                score = Math.Round(result.Score, 4),
            }));
        });

        api.MapPost("/memory", async (StoreMemoryRequest request, IMemoryService memory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "content is required" });
            }

            MemoryKind kind = Enum.TryParse<MemoryKind>(request.Kind, ignoreCase: true, out MemoryKind parsed)
                ? parsed
                : MemoryKind.LongTerm;

            Guid id = await memory.StoreAsync(new MemoryEntry
            {
                Kind = kind,
                Content = request.Content,
                Metadata = request.Metadata ?? new Dictionary<string, string>(),
            });

            return Results.Ok(new { id });
        });

        api.MapGet("/memory/preferences", async (IMemoryService memory) =>
            Results.Ok(await memory.GetAllPreferencesAsync()));

        api.MapGet("/memory/preferences/{key}", async (string key, IMemoryService memory) =>
        {
            string? value = await memory.GetPreferenceAsync(key);
            return value is null
                ? Results.NotFound(new { error = $"No preference '{key}'." })
                : Results.Ok(new { key, value });
        });

        api.MapPost("/memory/preferences", async (PreferenceRequest request, IMemoryService memory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || request.Value is null)
            {
                return Results.BadRequest(new { error = "key and value are required" });
            }

            await memory.SetPreferenceAsync(request.Key, request.Value);
            return Results.Ok(new { key = request.Key, value = request.Value });
        });

        api.MapDelete("/memory/preferences/{key}", async (string key, IMemoryService memory) =>
        {
            bool deleted = await memory.RemovePreferenceAsync(key);
            return Results.Ok(new { deleted });
        });
    }

    private static void MapAIEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/ai/providers", async (IAIService ai) =>
            Results.Ok(await ai.GetProvidersAsync()));

        api.MapGet("/ai/models", (IOptions<AIOptions> options) =>
            Results.Ok(options.Value.Models.Select(model => new
            {
                model.Id,
                model.Provider,
                model.Model,
                model.DisplayName,
                model.IsDefault,
                model.Tags,
            })));

        api.MapPost("/ai/chat", async (ChatApiRequest request, IAIService ai) =>
        {
            try
            {
                ChatResponse response = await ai.ChatAsync(BuildChatRequest(request));
                return Results.Ok(new
                {
                    message = response.Message.Content,
                    role = response.Message.Role,
                    model = response.Model,
                    provider = response.Provider,
                    promptTokens = response.PromptTokens,
                    completionTokens = response.CompletionTokens,
                    finishReason = response.FinishReason,
                    durationMs = response.DurationMs,
                });
            }
            catch (Exception exception)
            {
                return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        api.MapPost("/ai/chat/stream", async (HttpContext context, ChatApiRequest request, IAIService ai, CancellationToken cancellationToken) =>
        {
            HttpResponse response = context.Response;
            response.Headers.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers.Connection = "keep-alive";

            try
            {
                await foreach (ChatChunk chunk in ai.StreamChatAsync(BuildChatRequest(request), cancellationToken))
                {
                    string payload = JsonSerializer.Serialize(
                        new { delta = chunk.Delta, model = chunk.Model, done = chunk.Done }, SerializerOptions);
                    await response.WriteAsync($"data: {payload}\n\n", Encoding.UTF8, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }

                await response.WriteAsync("data: [DONE]\n\n", Encoding.UTF8, cancellationToken);
            }
            catch (Exception exception)
            {
                await response.WriteAsync($"data: {{\"error\":{JsonSerializer.Serialize(exception.Message)}}}\n\n", Encoding.UTF8, cancellationToken);
            }
        });

        api.MapPost("/ai/sessions/{sessionId}/clear", async (string sessionId, IAIService ai) =>
        {
            await ai.ClearSessionAsync(sessionId);
            return Results.Ok(new { cleared = true, sessionId });
        });
    }

    private static Jarvis.SDK.AI.ChatRequest BuildChatRequest(ChatApiRequest request)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(ChatMessage.User(request.Prompt));
        }
        else if (request.Messages is { Count: > 0 })
        {
            messages.AddRange(request.Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new ChatMessage { Role = message.Role ?? "user", Content = message.Content! }));
        }

        if (messages.Count == 0)
        {
            throw new ArgumentException("Either 'prompt' or 'messages' must be provided.");
        }

        TaskKind taskKind = Enum.TryParse<TaskKind>(request.TaskKind, ignoreCase: true, out TaskKind parsed)
            ? parsed
            : TaskKind.Simple;

        return new Jarvis.SDK.AI.ChatRequest
        {
            Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model,
            TaskKind = taskKind,
            Messages = messages,
            SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? null : request.SessionId,
            SystemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            PreferLocal = request.PreferLocal ?? true,
        };
    }

    /// <summary>Body of <c>POST /api/plugins/{id}/commands/{command}</c>.</summary>
    public sealed class PluginCommandRequest
    {
        public Dictionary<string, object?>? Parameters { get; set; }
    }

    /// <summary>Body of <c>POST /api/memory</c>.</summary>
    public sealed class StoreMemoryRequest
    {
        public string? Content { get; set; }
        public string? Kind { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Body of <c>POST /api/memory/preferences</c>.</summary>
    public sealed class PreferenceRequest
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
    }
}
