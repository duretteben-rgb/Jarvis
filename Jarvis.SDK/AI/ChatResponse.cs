namespace Jarvis.SDK.AI;

/// <summary>
/// A complete response returned by the JARVIS AI engine.
/// </summary>
public sealed class ChatResponse
{
    /// <summary>The generated assistant message.</summary>
    public required ChatMessage Message { get; init; }

    /// <summary>Id of the model that produced the response.</summary>
    public required string Model { get; init; }

    /// <summary>Id of the provider that served the request.</summary>
    public required string Provider { get; init; }

    /// <summary>Tokens consumed by the prompt, when reported by the provider.</summary>
    public int? PromptTokens { get; init; }

    /// <summary>Tokens produced in the completion, when reported by the provider.</summary>
    public int? CompletionTokens { get; init; }

    /// <summary>Reason the provider stopped generating, when known.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Wall-clock time of the request in milliseconds.</summary>
    public long DurationMs { get; init; }
}
