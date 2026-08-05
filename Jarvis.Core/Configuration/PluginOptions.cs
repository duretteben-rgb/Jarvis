namespace Jarvis.Core.Configuration;

/// <summary>
/// Options bound from the <c>Plugins</c> configuration section.
/// </summary>
public sealed class PluginOptions
{
    /// <summary>Whether plugin discovery and loading is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory (relative to the runtime base directory) that is scanned for plugins.
    /// </summary>
    public string Directory { get; set; } = "plugins";

    /// <summary>Whether the plugins directory is watched for dynamic plugin changes.</summary>
    public bool WatchEnabled { get; set; } = true;

    /// <summary>Debounce delay, in milliseconds, before a directory change is processed.</summary>
    public int WatchDebounceMs { get; set; } = 1500;
}
