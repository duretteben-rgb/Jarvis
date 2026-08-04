using Jarvis.SDK.Events;

namespace Jarvis.Core.EventBus;

/// <summary>
/// Internal representation of a single event subscription.
/// </summary>
internal interface ISubscription
{
    Guid Id { get; }

    EventPriority Priority { get; }

    DateTimeOffset CreatedAt { get; }

    Task InvokeAsync(object @event, CancellationToken cancellationToken);
}

/// <summary>
/// Typed subscription wrapping a user supplied handler.
/// </summary>
internal sealed class Subscription<TEvent> : ISubscription where TEvent : IEvent
{
    private readonly Func<TEvent, CancellationToken, Task> _handler;

    public Subscription(Func<TEvent, CancellationToken, Task> handler, EventPriority priority)
    {
        _handler = handler;
        Priority = priority;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public EventPriority Priority { get; }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    public Task InvokeAsync(object @event, CancellationToken cancellationToken)
        => _handler((TEvent)@event, cancellationToken);
}
