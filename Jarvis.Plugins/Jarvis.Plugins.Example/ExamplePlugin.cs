using Jarvis.SDK.Events.SystemEvents;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.Example;

/// <summary>
/// Reference plugin demonstrating the JARVIS SDK contracts:
/// <list type="bullet">
///   <item>declares a <see cref="PluginManifest"/>,</item>
///   <item>uses <see cref="JarvisPluginBase"/> for lifecycle handling,</item>
///   <item>subscribes to <see cref="HeartbeatEvent"/> published by the runtime through the EventBus.</item>
/// </list>
/// </summary>
public sealed class ExamplePlugin : JarvisPluginBase
{
    private IDisposable? _heartbeatSubscription;
    private int _heartbeatCount;

    public ExamplePlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.example",
            Name = "JARVIS Example Plugin",
            Version = "1.0.0",
            Description = "Reference plugin demonstrating the JARVIS SDK plugin contracts.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 1, 0),
        };
    }

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation("{Plugin} ({Version}) is starting.",
            Manifest.Id, Manifest.Version);

        _heartbeatSubscription = Context.Host.EventBus.Subscribe<HeartbeatEvent>((heartbeat, _) =>
        {
            int count = Interlocked.Increment(ref _heartbeatCount);
            Context.Logger.LogDebug("Heartbeat tick {Count} received (process {Pid}).",
                count, heartbeat.ProcessId);

            if (count % 5 == 0)
            {
                Context.Logger.LogInformation("{Plugin} has observed {Count} heartbeats.",
                    Manifest.Id, count);
            }

            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation("{Plugin} is stopping after {Count} heartbeats.",
            Manifest.Id, _heartbeatCount);

        _heartbeatSubscription?.Dispose();
        _heartbeatSubscription = null;
        return Task.CompletedTask;
    }
}
