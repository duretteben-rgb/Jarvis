using Jarvis.SDK.Configuration;
using Jarvis.SDK.Host;
using Microsoft.Extensions.Logging;

namespace Jarvis.SDK.Plugins;

/// <summary>
/// Execution context handed to a plugin when it is initialized by the host. It carries
/// everything a plugin needs to interact with JARVIS OS in a controlled way.
/// </summary>
public sealed class PluginContext
{
    /// <summary>Metadata of the plugin owning this context.</summary>
    public required PluginManifest Manifest { get; init; }

    /// <summary>Absolute path of the directory the plugin was loaded from.</summary>
    public required string PluginDirectory { get; init; }

    /// <summary>Logger namespaced to this plugin.</summary>
    public required ILogger Logger { get; init; }

    /// <summary>Public host API (event bus, configuration, service locator, ...).</summary>
    public required IJarvisHost Host { get; init; }
}
