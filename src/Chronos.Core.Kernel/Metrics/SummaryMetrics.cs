namespace Chronos.Core.Kernel.Metrics;

/// <summary>
/// Key performance metrics for a trading run.
/// </summary>
public sealed record SummaryMetrics
{
    /// <summary>Net profit in account currency.</summary>
    public double NetProfit { get; init; }

    /// <summary>Percentage return relative to initial balance.</summary>
    public double ReturnPct { get; init; }

    /// <summary>Maximum drawdown percentage.</summary>
    public double MaxDrawdownPct { get; init; }

    /// <summary>Maximum daily drawdown percentage observed.</summary>
    public double MaxDailyDrawdownPct { get; init; }

    /// <summary>Total number of trades.</summary>
    public int TotalTrades { get; init; }

    /// <summary>Win rate percentage.</summary>
    public double WinRatePct { get; init; }

    /// <summary>Gross profit divided by gross loss.</summary>
    public double ProfitFactor { get; init; }

    /// <summary>Annualised Sharpe ratio based on trade returns.</summary>
    public double SharpeRatio { get; init; }

    /// <summary>Annualised Sortino ratio based on trade returns.</summary>
    public double SortinoRatio { get; init; }

    /// <summary>Return divided by maximum drawdown.</summary>
    public double CalmarRatio { get; init; }
}
