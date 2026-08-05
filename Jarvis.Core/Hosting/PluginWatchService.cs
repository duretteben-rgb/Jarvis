using System.Collections.Concurrent;
using Jarvis.Core.Configuration;
using Jarvis.Core.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Hosting;

/// <summary>
/// Watches the plugins directory and dynamically loads plugins that appear while the system is
/// running, and unloads plugins whose directories disappear. This provides hot plugin loading
/// without restarting the host.
/// </summary>
public sealed class PluginWatchService : IHostedService, IDisposable
{
    private readonly IPluginManager _pluginManager;
    private readonly JarvisConfiguration _configuration;
    private readonly ILogger<PluginWatchService> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<string, DateTime> _pendingLoads = new();

    private FileSystemWatcher? _watcher;
    private TimeSpan _debounce = TimeSpan.FromMilliseconds(1500);
    private bool _enabled;

    public PluginWatchService(
        IPluginManager pluginManager,
        JarvisConfiguration configuration,
        ILogger<PluginWatchService> logger)
    {
        _pluginManager = pluginManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        PluginOptions options = _configuration.Plugins;
        _enabled = options.WatchEnabled;
        _debounce = TimeSpan.FromMilliseconds(Math.Max(100, options.WatchDebounceMs));

        if (!_enabled)
        {
            _logger.LogDebug("Plugin watch service is disabled by configuration.");
            return Task.CompletedTask;
        }

        string rootDirectory = ResolvePluginDirectory(options.Directory);
        if (!Directory.Exists(rootDirectory))
        {
            _logger.LogDebug("Plugin watch service has no directory to watch: {Directory}.", rootDirectory);
            return Task.CompletedTask;
        }

        _watcher = new FileSystemWatcher(rootDirectory)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName,
        };
        _watcher.Created += OnDirectoryCreated;
        _watcher.Deleted += OnDirectoryDeleted;
        _watcher.Renamed += OnDirectoryRenamed;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Watching {Directory} for dynamic plugin changes.", rootDirectory);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _shutdown.Cancel();
        return Task.CompletedTask;
    }

    private void OnDirectoryCreated(object sender, FileSystemEventArgs args)
        => ScheduleLoad(args.FullPath);

    private void OnDirectoryDeleted(object sender, FileSystemEventArgs args)
        => _ = UnloadAsync(args.FullPath);

    private void OnDirectoryRenamed(object sender, RenamedEventArgs args)
    {
        _ = UnloadAsync(args.OldFullPath);
        ScheduleLoad(args.FullPath);
    }

    private void ScheduleLoad(string directory)
    {
        _pendingLoads[directory] = DateTime.UtcNow;
        _ = LoadAfterDebounceAsync(directory);
    }

    private async Task LoadAfterDebounceAsync(string directory)
    {
        try
        {
            // Debounce so file copies that are still in progress are not attempted too early.
            await Task.Delay(_debounce, _shutdown.Token);

            // If a newer event was scheduled, this run is stale.
            if (_pendingLoads.TryGetValue(directory, out DateTime scheduled)
                && DateTime.UtcNow - scheduled < _debounce)
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                return;
            }

            _pendingLoads.TryRemove(directory, out _);
            _logger.LogInformation("Dynamic load requested for {Directory}.", directory);
            await _pluginManager.LoadPluginAsync(directory, _shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic plugin load failed for {Directory}.", directory);
        }
    }

    private async Task UnloadAsync(string directory)
    {
        try
        {
            await Task.Delay(_debounce, _shutdown.Token);
            bool unloaded = await _pluginManager.UnloadPluginByDirectoryAsync(directory, _shutdown.Token);
            if (unloaded)
            {
                _logger.LogInformation("Dynamically unloaded plugin from {Directory}.", directory);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic plugin unload failed for {Directory}.", directory);
        }
    }

    private static string ResolvePluginDirectory(string configuredDirectory)
        => Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(AppContext.BaseDirectory, configuredDirectory);

    public void Dispose()
    {
        _watcher?.Dispose();
        _shutdown.Dispose();
    }
}
