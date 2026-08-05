namespace Jarvis.SDK.Memory;

/// <summary>
/// Public contract of the JARVIS memory system. Implemented by <c>Jarvis.Memory</c> and exposed
/// to plugins and modules through the host service locator.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Stores an entry in memory. An embedding is generated for the content so it can be found
    /// later by semantic search. Returns the entry id.
    /// </summary>
    Task<Guid> StoreAsync(MemoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single entry by id, or null when it does not exist.</summary>
    Task<MemoryEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Semantically searches memory for entries relevant to <paramref name="query"/> using
    /// local embeddings and vector search.
    /// </summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        int limit = 10,
        MemoryKind? kind = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent entries, newest first.</summary>
    Task<IReadOnlyList<MemoryEntry>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>Stores (or updates) a user preference.</summary>
    Task SetPreferenceAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Reads a user preference, or null when it does not exist.</summary>
    Task<string?> GetPreferenceAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes a user preference. Returns true when it existed.</summary>
    Task<bool> RemovePreferenceAsync(string key, CancellationToken cancellationToken = default);
}
