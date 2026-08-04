using System.Reflection;
using System.Runtime.Loader;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Result of discovering a plugin assembly on disk. Carries the plugin type plus the load
/// context and directory it belongs to.
/// </summary>
public sealed class PluginDescriptor
{
    public PluginDescriptor(Type pluginType, string directory, AssemblyLoadContext loadContext)
    {
        PluginType = pluginType;
        Directory = directory;
        LoadContext = loadContext;
    }

    /// <summary>Type implementing <c>IJarvisPlugin</c>.</summary>
    public Type PluginType { get; }

    /// <summary>Directory the plugin was loaded from.</summary>
    public string Directory { get; }

    /// <summary>Assembly load context that owns the plugin assembly.</summary>
    public AssemblyLoadContext LoadContext { get; }

    public void Unload() => LoadContext.Unload();
}
