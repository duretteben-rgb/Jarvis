namespace Jarvis.SDK.Memory;

/// <summary>
/// A unit of memory stored by the JARVIS memory system.
/// </summary>
public sealed class MemoryEntry
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Category of the entry.</summary>
    public MemoryKind Kind { get; init; } = MemoryKind.ShortTerm;

    /// <summary>Text content of the memory.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Free-form metadata attached to the entry.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC last modification time.</summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
