namespace Chronos.Core.Abstractions.Shared.Events;

/// <summary>
/// Published when an order is executed (open or close) in both simulated and live trading.
/// </summary>
public sealed record OrderExecutedEvent : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
    /// <summary>Trading symbol.</summary>
    public string Symbol { get; init; } = string.Empty;
    /// <summary>Order type (Buy, Sell, etc.).</summary>
    public string OrderType { get; init; } = string.Empty;
    /// <summary>Executed volume.</summary>
    public double Volume { get; init; }
    /// <summary>Execution price.</summary>
    public double Price { get; init; }
    /// <summary>True if this event represents a position opening, false if closing.</summary>
    public bool IsOpen { get; init; }
}
