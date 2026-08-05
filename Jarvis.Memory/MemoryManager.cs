using Jarvis.Memory.Configuration;
using Jarvis.Memory.Database;
using Jarvis.Memory.Embedding;
using Jarvis.Memory.Repository;
using Jarvis.Memory.VectorStore;
using Jarvis.SDK.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MemoryDb = Jarvis.Memory.Database.MemoryDatabase;

namespace Jarvis.Memory;

/// <summary>
/// Default implementation of <see cref="IMemoryService"/>. Wires together the SQLite repository,
/// the local embedding service and the vector search to provide a self-contained memory system
/// for JARVIS OS. Plugins and modules interact with this service through the host.
/// </summary>
public sealed class MemoryManager : IMemoryService, IDisposable
{
    private readonly MemoryDb _database;
    private readonly MemoryRepository _repository;
    private readonly HashEmbeddingService _embeddingService;
    private readonly int _maxSearchResults;
    private readonly ILogger<MemoryManager> _logger;

    public MemoryManager(
        MemoryDb database,
        MemoryRepository repository,
        HashEmbeddingService embeddingService,
        IOptions<MemoryOptions> options,
        ILogger<MemoryManager> logger)
    {
        _database = database;
        _repository = repository;
        _embeddingService = embeddingService;
        _maxSearchResults = Math.Max(1, options.Value.MaxSearchResults);
        _logger = logger;
    }

    public async Task<Guid> StoreAsync(MemoryEntry entry, CancellationToken cancellationToken = default)
    {
        float[] embedding = _embeddingService.Compute(entry.Content);
        await _repository.UpsertEntryAsync(entry, embedding, cancellationToken);
        _logger.LogDebug("Stored memory entry {EntryId} (kind {Kind}).", entry.Id, entry.Kind);
        return entry.Id;
    }

    public Task<MemoryEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetEntryAsync(id, cancellationToken);

    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        int limit = 10,
        MemoryKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<MemorySearchResult>();
        }

        float[] queryEmbedding = _embeddingService.Compute(query);
        IReadOnlyList<StoredEntry> candidates = await _repository.LoadEntriesForSearchAsync(kind, cancellationToken);
        int effectiveLimit = Math.Max(1, Math.Min(_maxSearchResults, limit));
        return VectorSearch.Rank(candidates, queryEmbedding, effectiveLimit);
    }

    public Task<IReadOnlyList<MemoryEntry>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default)
        => _repository.GetRecentEntriesAsync(Math.Max(1, limit), cancellationToken);

    public Task SetPreferenceAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _repository.SetPreferenceAsync(key, value, cancellationToken);
    }

    public Task<string?> GetPreferenceAsync(string key, CancellationToken cancellationToken = default)
        => _repository.GetPreferenceAsync(key, cancellationToken);

    public Task<bool> RemovePreferenceAsync(string key, CancellationToken cancellationToken = default)
        => _repository.RemovePreferenceAsync(key, cancellationToken);

    public void Dispose() => _database.Dispose();
}
