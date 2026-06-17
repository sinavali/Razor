namespace Chronos.Core.Kernel.Messaging;

/// <summary>
/// Base contract for all domain messages transported via <see cref="IMessageBus"/>.
/// Every message carries a creation timestamp for ordering and diagnostics.
/// </summary>
public interface IMessage
{
    /// <summary>UTC timestamp when the message was created.</summary>
    DateTime Timestamp { get; }
    /// <summary>Optional correlation identifier for linking related events.</summary>
    Guid? CorrelationId { get; }
    /// <summary>Optional unique event identifier for deduplication.</summary>
    string? EventId { get; }
}
