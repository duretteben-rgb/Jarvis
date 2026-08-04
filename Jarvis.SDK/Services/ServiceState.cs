namespace Jarvis.SDK.Services;

/// <summary>
/// Lifecycle state of an <see cref="IJarvisService"/> managed by the service manager.
/// </summary>
public enum ServiceState
{
    /// <summary>Registered but not started.</summary>
    Stopped,

    /// <summary>Currently starting.</summary>
    Starting,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Currently stopping.</summary>
    Stopping,

    /// <summary>Failed during a lifecycle transition.</summary>
    Faulted,
}
