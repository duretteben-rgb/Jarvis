namespace Jarvis.SDK.Plugins;

/// <summary>
/// Metadata describing a plugin. Every plugin exposes a manifest so the host can manage,
/// display and version-check it.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Stable, unique identifier of the plugin (e.g. <c>jarvis.example</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Human readable plugin name.</summary>
    public required string Name { get; init; }

    /// <summary>Semantic version of the plugin.</summary>
    public required string Version { get; init; }

    /// <summary>Short description of what the plugin does.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Plugin author.</summary>
    public string Author { get; init; } = "Unknown";

    /// <summary>Minimum JARVIS core version this plugin requires.</summary>
    public Version MinimumCoreVersion { get; init; } = new(1, 0, 0);

    /// <summary>Permissions the plugin requests. Reserved for the future permission system.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();

    /// <summary>Ids of other plugins this plugin depends on.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
}
