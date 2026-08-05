using Jarvis.Memory.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.Memory.Database;

/// <summary>
/// Owns the SQLite connection and schema for the memory system. All operations are serialized
/// through a single shared connection, which is safe and sufficient for a local personal
/// assistant database.
/// </summary>
public sealed class MemoryDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<MemoryDatabase> _logger;
    private bool _disposed;

    public MemoryDatabase(IOptions<MemoryOptions> options, ILogger<MemoryDatabase> logger)
    {
        _logger = logger;
        MemoryOptions memoryOptions = options.Value;

        string databasePath = ResolvePath(memoryOptions.DatabasePath);
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Cache = SqliteCacheMode.Shared,
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();
        InitializeSchema();

        _logger.LogInformation("JARVIS memory database ready at {Path}.", databasePath);
    }

    /// <summary>
    /// Runs an operation against the shared connection. Operations are serialized so concurrent
    /// writers cannot corrupt the database.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<SqliteConnection, T> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return operation(_connection);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void InitializeSchema()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = DatabaseSchema.Schema;
        command.ExecuteNonQuery();
    }

    private static string ResolvePath(string configuredPath)
        => Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
        _gate.Dispose();
    }
}
