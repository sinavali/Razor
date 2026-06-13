using System.Collections.Concurrent;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Messaging;

/// <summary>
/// Default implementation of <see cref="IMessageBus"/>.
/// Thread‑safe and suitable for production use.
/// </summary>
public sealed class MessageBus : IMessageBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly Lock _subscriptionLock = new();
    private readonly ConcurrentDictionary<string, long> _recentEventIds = new();  // stored as Environment.TickCount64
    private readonly Timer _cleanupTimer;
    private readonly long _dedupWindowMilliseconds;
    private readonly long _cleanupIntervalMilliseconds;

    /// <summary>
    /// Creates a new message bus.
    /// </summary>
    /// <param name="dedupWindowSeconds">Maximum age of an EventId to consider it a duplicate (default 60 s).</param>
    /// <param name="cleanupIntervalSeconds">Interval between internal cleanup scans (default 60 s).</param>
    public MessageBus(int dedupWindowSeconds = 60, int cleanupIntervalSeconds = 60)
    {
        _dedupWindowMilliseconds = (long)dedupWindowSeconds * 1000;
        _cleanupIntervalMilliseconds = (long)cleanupIntervalSeconds * 1000;

        _cleanupTimer = new Timer(_ =>
        {
            long cutoff = Environment.TickCount64 - _dedupWindowMilliseconds;
            foreach (var key in _recentEventIds.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList())
            {
                _recentEventIds.TryRemove(key, out long _);
            }
        }, null, TimeSpan.FromMilliseconds(_cleanupIntervalMilliseconds), TimeSpan.FromMilliseconds(_cleanupIntervalMilliseconds));
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

        Delegate[] snapshot;
        lock (_subscriptionLock)
        {
            snapshot = [.. handlers];
        }

        foreach (var handler in snapshot)
        {
            ((Action<T>)handler)(message);
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(handler);
        var type = typeof(T);
        lock (_subscriptionLock)
        {
            var handlers = _handlers.GetOrAdd(type, _ => []);
            handlers.Add(handler);
        }
        return new Unsubscriber(() =>
        {
            lock (_subscriptionLock)
            {
                if (_handlers.TryGetValue(type, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _handlers.TryRemove(type, out _);
                    }
                }
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose() => _cleanupTimer.Dispose();

    private sealed class Unsubscriber(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
