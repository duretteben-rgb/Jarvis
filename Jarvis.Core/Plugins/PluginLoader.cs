using System.Reflection;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Scans directories for plugin assemblies. A plugin is expected to live in its own
/// directory named after the assembly (e.g. <c>plugins/My.Plugin/My.Plugin.dll</c>).
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers plugins found in the sub-directories of <paramref name="pluginsRootDirectory"/>.
    /// Corrupt or unloadable assemblies are logged and skipped.
    /// </summary>
    public IReadOnlyList<PluginDescriptor> Discover(string pluginsRootDirectory)
    {
        var descriptors = new List<PluginDescriptor>();

        if (!Directory.Exists(pluginsRootDirectory))
        {
            _logger.LogWarning("Plugin directory {Directory} does not exist.", pluginsRootDirectory);
            return descriptors;
        }

        foreach (string directory in Directory.EnumerateDirectories(pluginsRootDirectory))
        {
            PluginDescriptor? descriptor = DiscoverSingle(directory);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    /// <summary>
    /// Discovers a single plugin located in <paramref name="pluginDirectory"/>, or null when the
    /// directory does not contain a loadable plugin assembly.
    /// </summary>
    public PluginDescriptor? DiscoverSingle(string pluginDirectory)
    {
        string assemblyName = Path.GetFileName(pluginDirectory);
        string assemblyPath = Path.Combine(pluginDirectory, assemblyName + ".dll");

        if (!File.Exists(assemblyPath))
        {
            _logger.LogDebug("Skipping {Directory}: no {Assembly} assembly found.", pluginDirectory, assemblyPath);
            return null;
        }

        try
        {
            var context = new PluginLoadContext(pluginDirectory);
            Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);

            Type? pluginType = assembly.GetTypes()
                .Where(type =>
                    typeof(IJarvisPlugin).IsAssignableFrom(type) &&
                    type is { IsAbstract: false, IsInterface: false })
                .FirstOrDefault();

            if (pluginType is null)
            {
                context.Unload();
                _logger.LogDebug("No IJarvisPlugin implementation found in {Assembly}.", assemblyPath);
                return null;
            }

            _logger.LogDebug("Discovered plugin {PluginType} from {Assembly}.", pluginType.FullName, assemblyPath);
            return new PluginDescriptor(pluginType, pluginDirectory, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover plugin in {Directory}.", pluginDirectory);
            return null;
        }
    }
}
