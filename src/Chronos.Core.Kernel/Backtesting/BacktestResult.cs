using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>
/// Immutable result of a completed backtest.
/// </summary>
public sealed record BacktestResult
{
    /// <summary>Final account balance.</summary>
    public double Balance
    {
        get; init;
    }

    /// <summary>Final equity (balance + floating PnL).</summary>
    public double Equity
    {
        get; init;
    }

    /// <summary>Maximum drawdown percentage observed.</summary>
    public double Drawdown
    {
        get; init;
    }

    /// <summary>Maximum daily drawdown percentage observed.</summary>
    public double DailyDrawdown
    {
        get; init;
    }

    /// <summary>Total number of trades (full + partial closes).</summary>
    public int TotalTrades
    {
        get; init;
    }

    /// <summary>Complete trade history in chronological order.</summary>
    public IReadOnlyList<Position> History { get; init; } = [];
}
