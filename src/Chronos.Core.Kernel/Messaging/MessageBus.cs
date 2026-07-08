using System.Collections.Concurrent;

namespace Chronos.Core.Kernel.Messaging;

/// <summary>
/// Default implementation of <see cref="IMessageBus"/>.
/// Thread‑safe, with deduplication and clean‑up.
/// Uses <see cref="WeakReference{T}"/> to avoid memory leaks from subscribers.
/// </summary>
public sealed class MessageBus : IMessageBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, List<WeakReference<Delegate>>> _handlers = new();
    private readonly Lock _subscriptionLock = new();
    private readonly ConcurrentDictionary<string, long> _recentEventIds = new();
    private readonly Timer _cleanupTimer;
    private readonly long _dedupWindowMilliseconds;
    private readonly long _cleanupIntervalMilliseconds;

    /// <summary>
    /// Creates a new message bus.
    /// </summary>
    public MessageBus(int dedupWindowSeconds = 60, int cleanupIntervalSeconds = 60)
    {
        _dedupWindowMilliseconds = (long)dedupWindowSeconds * 1000;
        _cleanupIntervalMilliseconds = (long)cleanupIntervalSeconds * 1000;

        _cleanupTimer = new Timer(_ =>
        {
            long cutoff = Environment.TickCount64 - _dedupWindowMilliseconds;
            foreach (var key in _recentEventIds.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList())
            {
                _recentEventIds.TryRemove(key, out var _);
            }

            // Clean up dead weak references.
            lock (_subscriptionLock)
            {
                foreach (var type in _handlers.Keys.ToList())
                {
                    if (_handlers.TryGetValue(type, out var list))
                    {
                        // Remove dead weak references.
                        list.RemoveAll(wr => !wr.TryGetTarget(out var _));
                        if (list.Count == 0)
                        {
                            _handlers.TryRemove(type, out var _);
                        }
                    }
                }
            }
        }, null, Timeout.Infinite, Timeout.Infinite);

        _cleanupTimer.Change(
            TimeSpan.FromMilliseconds(_cleanupIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_cleanupIntervalMilliseconds));
    }

    /// <inheritdoc/>
    public void Publish<T>(T message) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.EventId != null)
        {
            long now = Environment.TickCount64;
            if (_recentEventIds.TryGetValue(message.EventId, out long lastSeen) &&
                (now - lastSeen) < _dedupWindowMilliseconds)
            {
                return;
            }

            _recentEventIds[message.EventId] = now;
        }

        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            return;
        }

        // Take a snapshot of alive handlers.
        List<Delegate> aliveDelegates = new();
        lock (_subscriptionLock)
        {
            foreach (var weakRef in handlers)
            {
                if (weakRef.TryGetTarget(out var handler))
                {
                    aliveDelegates.Add(handler);
                }
            }
        }

        foreach (var handler in aliveDelegates)
        {
            try
            {
                ((Action<T>)handler)(message);
            }
#pragma warning disable CA1031 // Reason: Subscribers must not crash the bus.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                System.Diagnostics.Trace.TraceError($"MessageBus subscriber error: {ex}");
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(handler);
        var type = typeof(T);

        lock (_subscriptionLock)
        {
            var handlers = _handlers.GetOrAdd(type, _ => new List<WeakReference<Delegate>>());
            handlers.Add(new WeakReference<Delegate>(handler));
        }

        return new Unsubscriber(() =>
        {
            lock (_subscriptionLock)
            {
                if (_handlers.TryGetValue(type, out var list))
                {
                    // Use ReferenceEquals for delegate comparison to avoid CS0252.
                    list.RemoveAll(wr => !wr.TryGetTarget(out var existing) || !ReferenceEquals(existing, handler));
                    if (list.Count == 0)
                    {
                        _handlers.TryRemove(type, out var _);
                    }
                }
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private sealed class Unsubscriber(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
