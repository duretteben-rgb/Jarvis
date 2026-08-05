namespace Jarvis.SDK.AI;

/// <summary>
/// A streamed piece of an assistant response.
/// </summary>
public sealed class ChatChunk
{
    /// <summary>Incremental text delta since the previous chunk.</summary>
    public required string Delta { get; init; }

    /// <summary>Id of the model that produced this chunk.</summary>
    public string? Model { get; init; }

    /// <summary>True when this is the final chunk of the response.</summary>
    public bool Done { get; init; }
}
