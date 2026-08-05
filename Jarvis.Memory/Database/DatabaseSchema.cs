namespace Jarvis.Memory.Database;

/// <summary>
/// SQLite schema used by the JARVIS memory system.
/// </summary>
public static class DatabaseSchema
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS memory_entries (
            id TEXT PRIMARY KEY,
            kind INTEGER NOT NULL,
            content TEXT NOT NULL,
            metadata TEXT NOT NULL DEFAULT '{}',
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            embedding BLOB NULL
        );

        CREATE TABLE IF NOT EXISTS memory_preferences (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_memory_entries_kind ON memory_entries(kind);
        CREATE INDEX IF NOT EXISTS idx_memory_entries_created_at ON memory_entries(created_at);
        """;
}
