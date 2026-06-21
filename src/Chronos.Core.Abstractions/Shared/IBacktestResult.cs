namespace Chronos.Core.Abstractions.Shared;

/// <summary>Minimal backtest result contract for hook contexts.</summary>
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
