namespace Jarvis.SDK.Memory;

/// <summary>
/// A memory entry returned by a semantic search, together with its similarity score.
/// </summary>
public sealed class MemorySearchResult
{
    public MemorySearchResult(MemoryEntry entry, double score)
    {
        Entry = entry;
        Score = score;
    }

    /// <summary>The matched memory entry.</summary>
    public MemoryEntry Entry { get; }

    /// <summary>Similarity score in the range [0, 1]; higher means more relevant.</summary>
    public double Score { get; }
}
