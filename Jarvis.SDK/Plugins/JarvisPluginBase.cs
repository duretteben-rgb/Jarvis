using Jarvis.SDK.Events.SystemEvents;

namespace Jarvis.SDK.Plugins;

/// <summary>
/// Convenience base class for plugins. Handles state tracking and exposes no-op overrides so
/// plugin authors only implement the lifecycle steps they need.
/// </summary>
public abstract class JarvisPluginBase : IJarvisPlugin
{
    private bool _disposed;

    /// <summary>Execution context assigned by the host during initialization.</summary>
    protected PluginContext Context { get; private set; } = null!;

    /// <inheritdoc />
    public virtual PluginManifest Manifest { get; protected set; } = null!;

    /// <inheritdoc />
    public virtual IReadOnlyList<PluginCommand> Commands => Array.Empty<PluginCommand>();

    /// <summary>Current lifecycle state of this plugin.</summary>
    public PluginLifecycleState State { get; private set; } = PluginLifecycleState.Detected;

    /// <inheritdoc />
    public async Task InitializeAsync(PluginContext context, CancellationToken cancellationToken = default)
    {
        Context = context;
        State = PluginLifecycleState.Initialized;
        await OnInitializeAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        State = PluginLifecycleState.Starting;
        await OnStartAsync(cancellationToken);
        State = PluginLifecycleState.Running;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        State = PluginLifecycleState.Stopping;
        await OnStopAsync(cancellationToken);
        State = PluginLifecycleState.Stopped;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await OnDisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public virtual Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        => throw new PluginException(Manifest.Id, $"Unknown command '{command}'.");

    /// <summary>Override to run setup logic after the context is assigned.</summary>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Override to start the plugin's work.</summary>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Override to stop the plugin's work gracefully.</summary>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Override to release unmanaged resources.</summary>
    protected virtual ValueTask OnDisposeAsync() => ValueTask.CompletedTask;
}
