namespace Chronos.Core.Abstractions.Shared.Events;

/// <summary>
/// Published when a live trading session ends, carrying final performance metrics.
/// </summary>
public sealed record LiveSessionEndedEvent : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
    /// <summary>Final account balance.</summary>
    public double FinalBalance { get; init; }
    /// <summary>Final equity.</summary>
    public double FinalEquity { get; init; }
    /// <summary>Maximum drawdown observed during the session.</summary>
    public double MaxDrawdownPct { get; init; }
    /// <summary>Maximum daily drawdown observed during the session.</summary>
    public double MaxDailyDrawdownPct { get; init; }
    /// <summary>Total number of trades during the session.</summary>
    public int TotalTrades { get; init; }
}
