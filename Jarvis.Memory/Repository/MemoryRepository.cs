using System.Globalization;
using Jarvis.Memory.Database;
using Jarvis.SDK.Memory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jarvis.Memory.Repository;

/// <summary>
/// Low-level CRUD for memory entries and user preferences on top of the SQLite database.
/// All data access is serialized through the shared <see cref="MemoryDatabase"/> connection.
/// </summary>
public sealed class MemoryRepository
{
    private readonly MemoryDatabase _database;
    private readonly ILogger<MemoryRepository> _logger;

    public MemoryRepository(MemoryDatabase database, ILogger<MemoryRepository> logger)
    {
        _database = database;
        _logger = logger;
    }

    public Task UpsertEntryAsync(MemoryEntry entry, float[]? embedding, CancellationToken cancellationToken = default)
        => _database.ExecuteAsync<int>(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO memory_entries (id, kind, content, metadata, created_at, updated_at, embedding)
                    VALUES ($id, $kind, $content, $metadata, $created, $updated, $embedding)
                    ON CONFLICT(id) DO UPDATE SET
                        content = excluded.content,
                        metadata = excluded.metadata,
                        updated_at = excluded.updated_at,
                        embedding = excluded.embedding;
                    """;

                command.Parameters.AddWithValue("$id", entry.Id.ToString());
                command.Parameters.AddWithValue("$kind", (int)entry.Kind);
                command.Parameters.AddWithValue("$content", entry.Content);
                command.Parameters.AddWithValue("$metadata", SerializeMetadata(entry.Metadata));
                command.Parameters.AddWithValue("$created", entry.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$updated", entry.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$embedding", embedding is null ? DBNull.Value : FloatArrayToBlob(embedding));

                return command.ExecuteNonQuery();
            },
            cancellationToken);

    public async Task<MemoryEntry?> GetEntryAsync(Guid id, CancellationToken cancellationToken = default)
        => await _database.ExecuteAsync<MemoryEntry?>(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT id, kind, content, metadata, created_at, updated_at FROM memory_entries WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id.ToString());

                using SqliteDataReader reader = command.ExecuteReader();
                return reader.Read() ? ReadEntry(reader) : null;
            },
            cancellationToken);

    /// <summary>
    /// Loads entries (optionally restricted to a kind) together with their stored embeddings,
    /// for vector search. Returns empty vectors for entries that have no embedding.
    /// </summary>
    public async Task<IReadOnlyList<StoredEntry>> LoadEntriesForSearchAsync(MemoryKind? kind, CancellationToken cancellationToken = default)
        => await _database.ExecuteAsync<IReadOnlyList<StoredEntry>>(
            connection =>
            {
                var results = new List<StoredEntry>();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = kind.HasValue
                    ? "SELECT id, kind, content, metadata, created_at, updated_at, embedding FROM memory_entries WHERE kind = $kind;"
                    : "SELECT id, kind, content, metadata, created_at, updated_at, embedding FROM memory_entries;";
                if (kind.HasValue)
                {
                    command.Parameters.AddWithValue("$kind", (int)kind.Value);
                }

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    MemoryEntry entry = ReadEntry(reader);
                    float[] embedding = reader.IsDBNull(6) ? Array.Empty<float>() : BlobToFloatArray((byte[])reader[6]);
                    results.Add(new StoredEntry(entry, embedding));
                }

                return results;
            },
            cancellationToken);

    public async Task<IReadOnlyList<MemoryEntry>> GetRecentEntriesAsync(int limit, CancellationToken cancellationToken = default)
        => await _database.ExecuteAsync<IReadOnlyList<MemoryEntry>>(
            connection =>
            {
                var results = new List<MemoryEntry>();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id, kind, content, metadata, created_at, updated_at
                    FROM memory_entries
                    ORDER BY created_at DESC
                    LIMIT $limit;
                    """;
                command.Parameters.AddWithValue("$limit", limit);

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(ReadEntry(reader));
                }

                return results;
            },
            cancellationToken);

    public async Task SetPreferenceAsync(string key, string value, CancellationToken cancellationToken = default)
        => await _database.ExecuteAsync<int>(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO memory_preferences (key, value, updated_at)
                    VALUES ($key, $value, $updated)
                    ON CONFLICT(key) DO UPDATE SET
                        value = excluded.value,
                        updated_at = excluded.updated_at;
                    """;

                string now = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue("$key", key);
                command.Parameters.AddWithValue("$value", value);
                command.Parameters.AddWithValue("$updated", now);
                return command.ExecuteNonQuery();
            },
            cancellationToken);

    public async Task<string?> GetPreferenceAsync(string key, CancellationToken cancellationToken = default)
        => await _database.ExecuteAsync<string?>(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM memory_preferences WHERE key = $key;";
                command.Parameters.AddWithValue("$key", key);

                object? result = command.ExecuteScalar();
                return result as string;
            },
            cancellationToken);

    public async Task<bool> RemovePreferenceAsync(string key, CancellationToken cancellationToken = default)
        => await _database.ExecuteAsync<bool>(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM memory_preferences WHERE key = $key;";
                command.Parameters.AddWithValue("$key", key);
                return command.ExecuteNonQuery() > 0;
            },
            cancellationToken);

    private static MemoryEntry ReadEntry(SqliteDataReader reader)
    {
        MemoryKind kind = (MemoryKind)reader.GetInt32(1);
        string? metadata = reader.IsDBNull(3) ? null : reader.GetString(3);
        string? createdAt = reader.IsDBNull(4) ? null : reader.GetString(4);
        string? updatedAt = reader.IsDBNull(5) ? null : reader.GetString(5);

        return new MemoryEntry
        {
            Id = Guid.Parse(reader.GetString(0)),
            Kind = kind,
            Content = reader.GetString(2),
            Metadata = DeserializeMetadata(metadata),
            CreatedAt = ParseTimestamp(createdAt),
            UpdatedAt = ParseTimestamp(updatedAt),
        };
    }

    private static DateTimeOffset ParseTimestamp(string? value)
        => value is null
            ? DateTimeOffset.MinValue
            : DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
                ? parsed
                : DateTimeOffset.MinValue;

    private static string SerializeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var items = metadata.Select(pair => $"\"{Escape(pair.Key)}\":\"{Escape(pair.Value)}\"");
        return "{" + string.Join(",", items) + "}";
    }

    private static IReadOnlyDictionary<string, string> DeserializeMetadata(string? json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(json) || json.Length < 2)
        {
            return result;
        }

        string body = json[1..^1];
        int index = 0;
        while (index < body.Length)
        {
            index = ReadString(body, index, out string key);
            index = SkipToColon(body, index);
            index = ReadString(body, index, out string value);
            result[key] = value;
            index = SkipToComma(body, index);
        }

        return result;
    }

    private static int ReadString(string text, int index, out string value)
    {
        while (index < text.Length && text[index] != '"')
        {
            index++;
        }

        index++; // opening quote
        int start = index;
        while (index < text.Length && text[index] != '"')
        {
            index++;
        }

        value = Unescape(text[start..index]);
        return index + 1; // past closing quote
    }

    private static int SkipToColon(string text, int index)
    {
        while (index < text.Length && text[index] != ':')
        {
            index++;
        }

        return index + 1;
    }

    private static int SkipToComma(string text, int index)
    {
        while (index < text.Length && text[index] != ',')
        {
            index++;
        }

        return index + 1;
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Unescape(string value)
        => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static byte[] FloatArrayToBlob(float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BlobToFloatArray(byte[] bytes)
    {
        float[] values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }
}
