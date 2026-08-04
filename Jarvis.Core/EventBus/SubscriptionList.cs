using Jarvis.SDK.Events;

namespace Jarvis.Core.EventBus;

/// <summary>
/// Internal, thread-safe collection of subscriptions for a single event type.
/// </summary>
internal sealed class SubscriptionList
{
    private readonly object _gate = new();
    private readonly List<ISubscription> _subscriptions = new();

    public void Add(ISubscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }
    }

    public void Remove(ISubscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    /// <summary>Returns an ordered snapshot of the current subscriptions.</summary>
    public IReadOnlyList<ISubscription> Snapshot()
    {
        lock (_gate)
        {
            return _subscriptions
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.CreatedAt)
                .ToArray();
        }
    }
}
