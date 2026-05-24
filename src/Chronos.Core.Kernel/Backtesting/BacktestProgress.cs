namespace Chronos.Core.Kernel.Backtesting;

/// <summary>
/// Progress information emitted during a backtest.
/// </summary>
public sealed record BacktestProgress(
    double PercentComplete,
    string Message
);
