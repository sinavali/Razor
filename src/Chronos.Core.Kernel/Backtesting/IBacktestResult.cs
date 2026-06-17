using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>Result of a completed backtest.</summary>
public interface IBacktestResult
{
    /// <summary>Final account balance.</summary>
    double Balance { get; }

    /// <summary>Final equity.</summary>
    double Equity { get; }

    /// <summary>Maximum drawdown observed.</summary>
    double Drawdown { get; }

    /// <summary>Maximum daily drawdown observed.</summary>
    double DailyDrawdown { get; }

    /// <summary>Total number of trades.</summary>
    int TotalTrades { get; }

    /// <summary>Chronological trade history.</summary>
    IReadOnlyList<Position> History { get; }
}
