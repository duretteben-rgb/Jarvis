namespace Jarvis.SDK.Events;

/// <summary>
/// Decoupled, in-process publish/subscribe contract used for communication between every
/// module of JARVIS OS (core services, runtime, plugins and UI).
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to every subscriber registered for <typeparamref name="TEvent"/>.
    /// Subscribers are invoked in priority order and are isolated from each other: a failing
    /// subscriber never prevents the others from receiving the event.
    /// </summary>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;

    /// <summary>
    /// Registers an asynchronous handler for a specific event type.
    /// </summary>
    /// <param name="handler">Handler invoked whenever an event of type <typeparamref name="TEvent"/> is published.</param>
    /// <param name="priority">Relative ordering among subscribers of the same event type.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    IDisposable Subscribe<TEvent>(
        Func<TEvent, CancellationToken, Task> handler,
        EventPriority priority = EventPriority.Normal) where TEvent : IEvent;
}
