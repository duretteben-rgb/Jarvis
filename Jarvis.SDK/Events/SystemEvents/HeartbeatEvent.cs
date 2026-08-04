using Jarvis.SDK.Events;

namespace Jarvis.SDK.Events.SystemEvents;

/// <summary>
/// Periodic system signal published by the JARVIS runtime. It is a lightweight way for any
/// module (including plugins) to observe that the system is alive.
/// </summary>
public sealed class HeartbeatEvent : JarvisEvent
{
    public HeartbeatEvent()
        : base("Jarvis.Runtime")
    {
    }

    /// <summary>Id of the operating system process hosting the runtime.</summary>
    public int ProcessId { get; } = Environment.ProcessId;

    /// <summary>Unique id for this heartbeat tick.</summary>
    public Guid TickId { get; } = Guid.NewGuid();
}
