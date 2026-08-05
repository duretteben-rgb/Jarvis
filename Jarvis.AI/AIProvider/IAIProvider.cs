using Jarvis.SDK.AI;

namespace Jarvis.AI.AIProvider;

/// <summary>
/// Abstraction over a concrete AI model backend. Implementations wrap local runtimes (Ollama)
/// or cloud APIs (OpenAI-compatible) behind a single chat contract, so the rest of the system
/// never cares which provider answered.
/// </summary>
public interface IAIProvider
{
    /// <summary>Stable provider id (e.g. <c>ollama</c>, <c>openai</c>).</summary>
    string Id { get; }

    /// <summary>Human readable provider name.</summary>
    string DisplayName { get; }

    /// <summary>True when the provider runs on this machine and needs no API key.</summary>
    bool IsLocal { get; }

    /// <summary>
    /// Probes availability. Implementations should fail fast (short timeout) so the router can
    /// skip unavailable providers without blocking requests.
    /// </summary>
    Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the model ids currently offered by this provider.</summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Produces a complete, non-streaming completion.</summary>
    Task<ChatResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double? temperature,
        int? maxTokens,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a completion as text deltas.</summary>
    IAsyncEnumerable<ChatChunk> StreamChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double? temperature,
        int? maxTokens,
        CancellationToken cancellationToken = default);
}
