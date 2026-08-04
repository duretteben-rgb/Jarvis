namespace Jarvis.SDK.Events.SystemEvents;

/// <summary>
/// Lifecycle states a plugin goes through while being managed by the host.
/// </summary>
public enum PluginLifecycleState
{
    /// <summary>The plugin assembly was discovered on disk.</summary>
    Detected,

    /// <summary>The plugin was initialized with its context.</summary>
    Initialized,

    /// <summary>The plugin is starting.</summary>
    Starting,

    /// <summary>The plugin is running and ready to handle work.</summary>
    Running,

    /// <summary>The plugin is stopping.</summary>
    Stopping,

    /// <summary>The plugin was stopped.</summary>
    Stopped,

    /// <summary>The plugin failed during a lifecycle transition.</summary>
    Failed,
}
