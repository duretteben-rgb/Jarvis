using Jarvis.SDK.Plugins;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Loads, initializes, starts and stops plugins. This is the heart of the JARVIS extension
/// system: every capability that ships as a plugin goes through this manager. Loading is
/// dynamic — plugins can be added or removed while the system is running.
/// </summary>
public interface IPluginManager
{
    /// <summary>Snapshot of all plugins currently loaded and running.</summary>
    IReadOnlyList<IJarvisPlugin> Plugins { get; }

    /// <summary>Management snapshot of every loaded plugin.</summary>
    IReadOnlyList<PluginInfo> GetPluginInfos();

    /// <summary>
    /// Discovers plugins on disk and brings them to the running state.
    /// </summary>
    Task LoadAndStartPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dynamically loads and starts a single plugin from a specific directory at runtime.
    /// Returns the plugin instance, or null when loading failed.
    /// </summary>
    Task<IJarvisPlugin?> LoadPluginAsync(string pluginDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dynamically stops and unloads a loaded plugin by id.
    /// </summary>
    Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dynamically stops and unloads a loaded plugin by its source directory.
    /// </summary>
    Task<bool> UnloadPluginByDirectoryAsync(string pluginDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops every running plugin and unloads their assemblies.
    /// </summary>
    Task StopAndUnloadPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a command declared by a loaded plugin and publishes a <c>PluginCommandEvent</c>.
    /// </summary>
    Task<object?> ExecuteCommandAsync(
        string pluginId,
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);
}
