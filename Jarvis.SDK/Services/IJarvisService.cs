namespace Jarvis.SDK.Services;

/// <summary>
/// Contract for a long-running internal service that the <c>Jarvis.Core.ServiceManager</c>
/// manages as part of the system lifecycle.
/// </summary>
public interface IJarvisService : IAsyncDisposable
{
    /// <summary>Human readable service name used in logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>Starts the service.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the service.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
