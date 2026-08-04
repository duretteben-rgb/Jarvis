using System.Reflection;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Scans a directory tree for plugin assemblies. A plugin is expected to live in its own
/// sub-directory named after the assembly (e.g. <c>plugins/My.Plugin/My.Plugin.dll</c>).
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers all plugin types found under <paramref name="pluginsRootDirectory"/>.
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
            string assemblyName = Path.GetFileName(directory);
            string assemblyPath = Path.Combine(directory, assemblyName + ".dll");

            if (!File.Exists(assemblyPath))
            {
                _logger.LogDebug("Skipping {Directory}: no {Assembly} assembly found.", directory, assemblyPath);
                continue;
            }

            try
            {
                var context = new PluginLoadContext(directory);
                Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);

                Type[] candidateTypes = assembly.GetTypes()
                    .Where(type =>
                        typeof(IJarvisPlugin).IsAssignableFrom(type) &&
                        type is { IsAbstract: false, IsInterface: false })
                    .ToArray();

                foreach (Type type in candidateTypes)
                {
                    descriptors.Add(new PluginDescriptor(type, directory, context));
                    _logger.LogDebug("Discovered plugin {PluginType} from {Assembly}.", type.FullName, assemblyPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover plugin in {Directory}.", directory);
            }
        }

        return descriptors;
    }
}
