namespace Jarvis.Memory.Configuration;

/// <summary>
/// Options bound from the <c>Memory</c> configuration section.
/// </summary>
public sealed class MemoryOptions
{
    /// <summary>Whether the memory system is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Path of the SQLite database file (relative paths resolve against the base directory).</summary>
    public string DatabasePath { get; set; } = "data/jarvis-memory.db";

    /// <summary>Default maximum number of results returned by a search.</summary>
    public int MaxSearchResults { get; set; } = 10;
}
