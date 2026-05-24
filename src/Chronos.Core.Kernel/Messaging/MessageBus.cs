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
    private readonly ConcurrentDictionary<string, DateTime> _recentEventIds = new();
    private readonly Timer _cleanupTimer;

    /// <inheritdoc/>
    public MessageBus()
    {
        _cleanupTimer = new Timer(_ =>
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-120);
            foreach (var key in _recentEventIds.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList())
                _recentEventIds.TryRemove(key, out DateTime _);
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <inheritdoc/>
    public void Publish<T>(T message) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.EventId != null && _recentEventIds.TryGetValue(message.EventId, out var last) && (DateTime.UtcNow - last).TotalSeconds < 60)
            return;

        if (message.EventId != null)
            _recentEventIds[message.EventId] = DateTime.UtcNow;

        if (!_handlers.TryGetValue(typeof(T), out var handlers))
            return;

        Delegate[] snapshot;
        lock (_subscriptionLock)
            snapshot = [.. handlers];

        foreach (var handler in snapshot)
            ((Action<T>)handler)(message);
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
                        _handlers.TryRemove(type, out _);
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
