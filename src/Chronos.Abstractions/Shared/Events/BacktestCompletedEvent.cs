namespace Chronos.Abstractions.Shared.Events;

/// <summary>
/// Published when a backtest completes, carrying full performance metrics.
/// </summary>
public sealed record BacktestCompletedEvent : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
    /// <summary>Net profit in account currency.</summary>
    public double NetProfit { get; init; }
    /// <summary>Percentage return relative to initial balance.</summary>
    public double ReturnPct { get; init; }
    /// <summary>Maximum drawdown percentage.</summary>
    public double MaxDrawdownPct { get; init; }
    /// <summary>Maximum daily drawdown percentage.</summary>
    public double MaxDailyDrawdownPct { get; init; }
    /// <summary>Total number of trades.</summary>
    public int TotalTrades { get; init; }
    /// <summary>Win rate percentage.</summary>
    public double WinRatePct { get; init; }
    /// <summary>Gross profit divided by gross loss.</summary>
    public double ProfitFactor { get; init; }
    /// <summary>Annualised Sharpe ratio.</summary>
    public double SharpeRatio { get; init; }
    /// <summary>Annualised Sortino ratio.</summary>
    public double SortinoRatio { get; init; }
}