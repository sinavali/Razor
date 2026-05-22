namespace Chronos.Abstractions.Shared;

/// <summary>
/// Lightweight in‑process message bus for cross‑component communication.
/// </summary>
public interface IMessageBus
{
    /// <summary>Publishes a message to all subscribed handlers of the specific message type.</summary>
    void Publish<T>(T message) where T : IMessage;
    /// <summary>Subscribes to messages of a specific type. Returns a disposable that unsubscribes.</summary>
    IDisposable Subscribe<T>(Action<T> handler) where T : IMessage;
}