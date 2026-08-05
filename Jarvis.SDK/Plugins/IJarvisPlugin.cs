using Jarvis.SDK.Events.SystemEvents;

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

    /// <summary>Commands this plugin exposes to the host.</summary>
    IReadOnlyList<PluginCommand> Commands { get; }

    /// <summary>Current lifecycle state of this plugin.</summary>
    PluginLifecycleState State { get; }

    /// <summary>
    /// Called once, after the plugin instance was created. The context gives the plugin access
    /// to the event bus, configuration and service locator.
    /// </summary>
    Task InitializeAsync(PluginContext context, CancellationToken cancellationToken = default);

    /// <summary>Starts the plugin so it can begin doing work.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Gracefully stops the plugin. The host disposes it afterwards.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a command declared in <see cref="Commands"/>. Plugins should throw
    /// <see cref="PluginException"/> for unknown commands or command failures.
    /// </summary>
    /// <param name="command">Name of the command to execute.</param>
    /// <param name="parameters">Optional free-form parameters for the command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Command result, or null when the command has no result.</returns>
    Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);
}
