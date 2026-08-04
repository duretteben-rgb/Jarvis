namespace Jarvis.SDK.Plugins;

/// <summary>
/// Contract implemented by every JARVIS plugin. The host drives the plugin through its
/// lifecycle: <see cref="InitializeAsync"/>, <see cref="StartAsync"/>, <see cref="StopAsync"/>,
/// then disposal.
/// </summary>
public interface IJarvisPlugin : IAsyncDisposable
{
    /// <summary>Metadata of this plugin.</summary>
    PluginManifest Manifest { get; }

    /// <summary>
    /// Called once, after the plugin instance was created. The context gives the plugin access
    /// to the event bus, configuration and service locator.
    /// </summary>
    Task InitializeAsync(PluginContext context, CancellationToken cancellationToken = default);

    /// <summary>Starts the plugin so it can begin doing work.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Gracefully stops the plugin. The host disposes it afterwards.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
