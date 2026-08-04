using Jarvis.SDK.Plugins;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Loads, initializes, starts and stops plugins. This is the heart of the JARVIS extension
/// system: every capability that ships as a plugin goes through this manager.
/// </summary>
public interface IPluginManager
{
    /// <summary>Snapshot of all plugins currently loaded and running.</summary>
    IReadOnlyList<IJarvisPlugin> Plugins { get; }

    /// <summary>
    /// Discovers plugins on disk and brings them to the running state.
    /// </summary>
    Task LoadAndStartPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops every running plugin and unloads their assemblies.
    /// </summary>
    Task StopAndUnloadPluginsAsync(CancellationToken cancellationToken = default);
}
