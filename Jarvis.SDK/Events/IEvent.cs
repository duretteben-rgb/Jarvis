namespace Jarvis.SDK.Events;

/// <summary>
/// Marker contract for every message that can flow through the <see cref="IEventBus"/>.
/// </summary>
public interface IEvent
{
    /// <summary>Unique identifier of the event instance.</summary>
    Guid EventId { get; }

    /// <summary>Timestamp (UTC) of when the event was created.</summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>Name of the module that produced the event, if known.</summary>
    string? Source { get; set; }

    /// <summary>
    /// Optional correlation identifier used to group events belonging to the same logical operation.
    /// </summary>
    Guid? CorrelationId { get; set; }
}
