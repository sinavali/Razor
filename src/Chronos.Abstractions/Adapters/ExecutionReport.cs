namespace Chronos.Abstractions.Adapters;

/// <summary>
/// Dynamic order update received from the exchange (via WebSocket user‑data streams).
/// </summary>
public sealed record ExecutionReport
{
    /// <summary>Broker ticket.</summary>
    public long Ticket { get; init; }
    /// <summary>Symbol.</summary>
    public string Symbol { get; init; } = string.Empty;
    /// <summary>Order type.</summary>
    public Chronos.Abstractions.Shared.OrderType Type { get; init; }
    /// <summary>Current execution state.</summary>
    public ExecutionState State { get; init; }
    /// <summary>Volume executed so far in this update.</summary>
    public double ExecutedVolume { get; init; }
    /// <summary>Average executed price.</summary>
    public double ExecutedPrice { get; init; }
    /// <summary>Volume remaining.</summary>
    public double RemainingVolume { get; init; }
    /// <summary>Commission charged for this update.</summary>
    public double Commission { get; init; }
    /// <summary>Realised P&amp;L for this update.</summary>
    public double RealizedPnL { get; init; }
    /// <summary>Timestamp (ticks).</summary>
    public long Timestamp { get; init; }
    /// <summary>User comment.</summary>
    public string Comment { get; init; } = string.Empty;
}