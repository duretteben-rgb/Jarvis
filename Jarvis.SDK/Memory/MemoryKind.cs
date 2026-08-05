namespace Jarvis.SDK.Memory;

/// <summary>
/// Category of a memory entry, mirroring the layers of the JARVIS memory system.
/// </summary>
public enum MemoryKind
{
    /// <summary>Conversation context, active tasks, transient state.</summary>
    ShortTerm,

    /// <summary>Preferences, habits, projects and acquired knowledge.</summary>
    LongTerm,

    /// <summary>An explicit user preference (key/value).</summary>
    Preference,

    /// <summary>Knowledge stored for semantic retrieval.</summary>
    Semantic,
}
