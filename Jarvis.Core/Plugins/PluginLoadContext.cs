using System.Reflection;
using System.Runtime.Loader;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Per-plugin assembly load context. Plugins are isolated from each other so a plugin can be
/// unloaded and updated without restarting the host. Assemblies already loaded by the host
/// (such as <c>Jarvis.SDK</c> and the framework) are shared to guarantee type identity.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly string _pluginDirectory;
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginDirectory)
        : base($"Jarvis.Plugin:{Path.GetFileName(pluginDirectory)}", isCollectible: true)
    {
        _pluginDirectory = pluginDirectory;
        _resolver = new AssemblyDependencyResolver(
            Path.Combine(pluginDirectory, Path.GetFileName(pluginDirectory) + ".dll"));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Prefer assemblies already loaded by the host (framework, SDK, shared dependencies).
        // This keeps plugin types and host types identical.
        Assembly? shared = Default.Assemblies
            .FirstOrDefault(assembly => assembly.GetName().Name == assemblyName.Name);
        if (shared is not null)
        {
            return shared;
        }

        // Fall back to the plugin's own dependency resolution (its deps.json or directory).
        string? path = _resolver.ResolveAssemblyToPath(assemblyName)
            ?? ProbePluginDirectory(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }

    private string? ProbePluginDirectory(AssemblyName assemblyName)
    {
        string candidate = Path.Combine(_pluginDirectory, assemblyName.Name + ".dll");
        return File.Exists(candidate) ? candidate : null;
    }
}
