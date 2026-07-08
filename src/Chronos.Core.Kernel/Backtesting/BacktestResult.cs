using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>
/// Immutable result of a completed backtest.
/// </summary>
public sealed record BacktestResult : IBacktestResult
{
    /// <inheritdoc/>
    public double Balance { get; init; }

    /// <inheritdoc/>
    public double Equity { get; init; }

    /// <inheritdoc/>
    public double Drawdown { get; init; }

    /// <inheritdoc/>
    public double DailyDrawdown { get; init; }

    /// <inheritdoc/>
    public int TotalTrades { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<Position> History { get; init; } = Array.Empty<Position>();
}
