using Jarvis.SDK.Services;

namespace Jarvis.Core.ServiceManager;

/// <summary>
/// Manages the lifecycle of internal <see cref="IJarvisService"/> instances registered in the system.
/// </summary>
public interface IServiceManager
{
    /// <summary>All registered services.</summary>
    IReadOnlyList<IJarvisService> Services { get; }

    /// <summary>Registers a service so it is started and stopped with the system.</summary>
    void Register(IJarvisService service);

    /// <summary>Starts every registered service. A single failing service does not block the others.</summary>
    Task StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops every registered service in reverse registration order.</summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);
}
