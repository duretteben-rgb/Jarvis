using Jarvis.SDK.Plugins;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Orders plugins by their declared dependencies and detects missing or circular dependencies.
/// Dependent plugins are loaded after the plugins they depend on.
/// </summary>
public static class PluginDependencyResolver
{
    /// <summary>
    /// Resolves plugin load order. Plugins with unsatisfied or circular dependencies are
    /// excluded from the result and reported in <c>Errors</c>.
    /// </summary>
    public static (IReadOnlyList<PluginDescriptor> Ordered, IReadOnlyList<string> Errors) Resolve(
        IReadOnlyList<PluginDescriptor> descriptors)
    {
        var errors = new List<string>();
        var catalog = new Dictionary<string, PluginNode>(StringComparer.Ordinal);

        foreach (PluginDescriptor descriptor in descriptors)
        {
            try
            {
                var manifest = ((IJarvisPlugin)Activator.CreateInstance(descriptor.PluginType)!).Manifest;
                if (!catalog.TryAdd(manifest.Id, new PluginNode(descriptor, manifest)))
                {
                    errors.Add($"Duplicate plugin id '{manifest.Id}' in '{descriptor.Directory}'.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Cannot inspect plugin in '{descriptor.Directory}': {ex.Message}");
            }
        }

        var ordered = new List<PluginDescriptor>();
        var visited = new Dictionary<string, int>(StringComparer.Ordinal); // 0 = visiting, 1 = done
        var skipped = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            if (skipped.Contains(id))
            {
                return false;
            }

            if (visited.TryGetValue(id, out int state))
            {
                if (state == 0)
                {
                    errors.Add($"Circular dependency detected involving plugin '{id}'.");
                    return false;
                }

                return true;
            }

            visited[id] = 0;
            foreach (string dependency in catalog[id].Manifest.Dependencies)
            {
                if (!catalog.ContainsKey(dependency) || !Visit(dependency))
                {
                    skipped.Add(id);
                    errors.Add($"Plugin '{id}' skipped: unsatisfied dependency '{dependency}'.");
                    visited[id] = 1;
                    return false;
                }
            }

            visited[id] = 1;
            ordered.Add(catalog[id].Descriptor);
            return true;
        }

        foreach (string id in catalog.Keys)
        {
            Visit(id);
        }

        return (ordered, errors);
    }

    private sealed record PluginNode(PluginDescriptor Descriptor, PluginManifest Manifest);
}
