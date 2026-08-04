using Jarvis.SDK.Services;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.ServiceManager;

/// <summary>
/// Default implementation of <see cref="IServiceManager"/>. Keeps services ordered by
/// registration, starts them forward and stops them in reverse order so dependencies shut
/// down before their dependents.
/// </summary>
public sealed class ServiceManager : IServiceManager
{
    private readonly ILogger<ServiceManager> _logger;
    private readonly object _gate = new();
    private readonly List<IJarvisService> _services = new();

    public ServiceManager(ILogger<ServiceManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<IJarvisService> Services
    {
        get
        {
            lock (_gate)
            {
                return _services.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public void Register(IJarvisService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        lock (_gate)
        {
            _services.Add(service);
            _logger.LogTrace("Registered service {Service}.", service.Name);
        }
    }

    /// <inheritdoc />
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var service in Services)
        {
            try
            {
                _logger.LogDebug("Starting service {Service}...", service.Name);
                await service.StartAsync(cancellationToken);
                _logger.LogInformation("Service {Service} started.", service.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service {Service} failed to start.", service.Name);
            }
        }
    }

    /// <inheritdoc />
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var service in Services.Reverse())
        {
            try
            {
                _logger.LogDebug("Stopping service {Service}...", service.Name);
                await service.StopAsync(cancellationToken);
                _logger.LogInformation("Service {Service} stopped.", service.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service {Service} failed to stop.", service.Name);
            }
        }
    }
}
