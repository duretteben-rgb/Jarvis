using System.Collections.Concurrent;
using Jarvis.SDK.Events;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.EventBus;

/// <summary>
/// In-process, asynchronous publish/subscribe bus. Communication between every module of
/// JARVIS OS flows through this bus:
///
/// <list type="bullet">
///   <item>Subscribers are invoked in <see cref="EventPriority"/> order.</item>
///   <item>Each subscriber is isolated: an exception in one handler never breaks the others.</item>
///   <item>Subscriptions are removed by disposing the returned <see cref="IDisposable"/>.</item>
/// </list>
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, SubscriptionList> _subscriptions = new();
    private readonly ILogger<EventBus> _logger;

    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (!_subscriptions.TryGetValue(typeof(TEvent), out var list))
        {
            return;
        }

        var subscribers = list.Snapshot();
        if (subscribers.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Publishing {EventType} to {SubscriberCount} subscriber(s).",
            typeof(TEvent).Name, subscribers.Count);

        foreach (var subscription in subscribers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await subscription.InvokeAsync(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Isolation: a failing subscriber must not prevent others from receiving the event.
                _logger.LogError(ex,
                    "Subscriber {SubscriptionId} failed while handling {EventType}.",
                    subscription.Id, typeof(TEvent).Name);
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(
        Func<TEvent, CancellationToken, Task> handler,
        EventPriority priority = EventPriority.Normal) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription<TEvent>(handler, priority);
        var list = _subscriptions.GetOrAdd(typeof(TEvent), static _ => new SubscriptionList());
        list.Add(subscription);

        _logger.LogTrace("Subscribed {SubscriptionId} to {EventType} with priority {Priority}.",
            subscription.Id, typeof(TEvent).Name, priority);

        return new SubscriptionDisposable(list, subscription);
    }

    /// <summary>
    /// Removes a subscription when disposed.
    /// </summary>
    private sealed class SubscriptionDisposable : IDisposable
    {
        private readonly SubscriptionList _list;
        private readonly ISubscription _subscription;
        private int _disposed;

        public SubscriptionDisposable(SubscriptionList list, ISubscription subscription)
        {
            _list = list;
            _subscription = subscription;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _list.Remove(_subscription);
            }
        }
    }
}
