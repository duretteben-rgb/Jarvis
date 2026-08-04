namespace Jarvis.SDK.Events;

/// <summary>
/// Abstract base class for all JARVIS events. Provides identity, timing and correlation by default.
/// </summary>
public abstract class JarvisEvent : IEvent
{
    protected JarvisEvent(string? source = null)
    {
        EventId = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        Source = source;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc />
    public string? Source { get; set; }

    /// <inheritdoc />
    public Guid? CorrelationId { get; set; }
}
