namespace Jarvis.SDK.AI;

/// <summary>
/// A request to the JARVIS AI engine. When <see cref="Model"/> is not set, the model router
/// selects the best provider and model for <see cref="TaskKind"/>.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>Optional explicit model id (as configured, e.g. <c>groq-llama</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Category of the task, used by the router when no model is forced.</summary>
    public TaskKind TaskKind { get; init; } = TaskKind.Simple;

    /// <summary>Conversation history. The last message is normally the user's prompt.</summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = Array.Empty<ChatMessage>();

    /// <summary>Optional system prompt prepended to the conversation.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Id of the conversation context used by the context manager. When set, history is
    /// tracked under this id and can be cleared later.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>Optional sampling temperature (0.0 - 2.0).</summary>
    public double? Temperature { get; init; }

    /// <summary>Optional maximum number of tokens to generate.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// When true (default), the router prefers a local model (e.g. Ollama) so the request can
    /// be served offline, and only falls back to a cloud provider when no local model is
    /// available.
    /// </summary>
    public bool PreferLocal { get; init; } = true;
}
