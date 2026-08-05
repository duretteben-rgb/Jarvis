namespace Jarvis.SDK.AI;

/// <summary>
/// Public contract of the JARVIS AI engine. Implemented by <c>Jarvis.AI</c> and exposed to
/// modules and plugins through the host. It abstracts over local and cloud model providers and
/// routes every request to the most suitable available model.
/// </summary>
public interface IAIService
{
    /// <summary>Lists the configured providers and their current health.</summary>
    Task<IReadOnlyList<AIProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a non-streaming completion for a chat request.</summary>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>Streams a completion as an async enumerable of text deltas.</summary>
    IAsyncEnumerable<ChatChunk> StreamChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>Clears the conversation context tracked under <paramref name="sessionId"/>.</summary>
    Task ClearSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
