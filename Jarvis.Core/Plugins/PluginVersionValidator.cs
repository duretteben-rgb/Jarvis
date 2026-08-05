using Jarvis.SDK;
using Jarvis.SDK.Plugins;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Validates plugin manifests against the running platform version and semantic versioning rules.
/// </summary>
public static class PluginVersionValidator
{
    /// <summary>
    /// Validates that a plugin manifest carries a well-formed version and a
    /// <c>MinimumCoreVersion</c> that is satisfied by the running core.
    /// </summary>
    /// <param name="manifest">Manifest to validate.</param>
    /// <param name="coreVersion">Version of the running platform (see <see cref="JarvisVersions.Platform"/>).</param>
    /// <returns>A tuple with the validation result and a human readable error, if any.</returns>
    public static (bool Ok, string? Error) Validate(PluginManifest manifest, string coreVersion)
    {
        if (!SemanticVersion.TryParse(manifest.Version, out _))
        {
            return (false, $"Plugin '{manifest.Id}' declares an invalid version '{manifest.Version}'.");
        }

        if (!SemanticVersion.TryParse(coreVersion, out SemanticVersion core))
        {
            return (false, $"Core version '{coreVersion}' is not a valid semantic version.");
        }

        var minimumCore = new SemanticVersion(
            manifest.MinimumCoreVersion.Major,
            manifest.MinimumCoreVersion.Minor,
            Math.Max(0, manifest.MinimumCoreVersion.Build));

        if (minimumCore > core)
        {
            return (false,
                $"Plugin '{manifest.Id}' requires core {minimumCore} or later, but the running core is {core}.");
        }

        return (true, null);
    }
}
