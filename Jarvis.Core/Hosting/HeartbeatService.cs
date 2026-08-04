using Jarvis.Core.Configuration;
using Jarvis.SDK.Configuration;
using Jarvis.SDK.Events;
using Jarvis.SDK.Events.SystemEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Hosting;

/// <summary>
/// Publishes a <see cref="HeartbeatEvent"/> on the system event bus at a configurable
/// interval. Demonstrates internal communication: any module or plugin can subscribe to the
/// heartbeat to observe that the system is alive.
/// </summary>
public sealed class HeartbeatService : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly TimeSpan _interval;

    public HeartbeatService(
        IEventBus eventBus,
        IJarvisConfiguration configuration,
        ILogger<HeartbeatService> logger)
    {
        _eventBus = eventBus;
        _logger = logger;

        JarvisOptions jarvis = configuration.GetSection<JarvisOptions>("Jarvis") ?? new JarvisOptions();
        _interval = TimeSpan.FromSeconds(Math.Max(1, jarvis.HeartbeatIntervalSeconds));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Heartbeat service started with interval {Interval}.", _interval);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _eventBus.PublishAsync(new HeartbeatEvent(), stoppingToken);
        }
    }
}
