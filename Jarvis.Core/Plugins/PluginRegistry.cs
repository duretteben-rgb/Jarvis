using Jarvis.SDK.Plugins;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Thread-safe registry shared by the plugin manager and the public host so the set of loaded
/// plugins can be observed without a circular dependency. Not part of the public API surface.
/// </summary>
public sealed class PluginRegistry
{
    private readonly object _gate = new();
    private readonly List<IJarvisPlugin> _plugins = new();

    public void Add(IJarvisPlugin plugin)
    {
        lock (_gate)
        {
            _plugins.Add(plugin);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _plugins.Clear();
        }
    }

    public IReadOnlyList<IJarvisPlugin> Snapshot()
    {
        lock (_gate)
        {
            return _plugins.ToArray();
        }
    }
}
