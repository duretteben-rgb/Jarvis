namespace Jarvis.SDK.Events;

/// <summary>
/// Ordering hint used by the <see cref="IEventBus"/> to invoke subscribers for the same event type.
/// Higher priority subscribers run first.
/// </summary>
public enum EventPriority
{
    /// <summary>Critical subscribers run before anyone else.</summary>
    Critical = 100,

    /// <summary>High priority subscribers.</summary>
    High = 75,

    /// <summary>Default priority.</summary>
    Normal = 50,

    /// <summary>Low priority subscribers run last.</summary>
    Low = 25,
}
