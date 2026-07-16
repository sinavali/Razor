namespace Razor.Core.Kernel.Backtesting;

/// <summary>
/// Runs a deterministic backtest against pre‑loaded tick data.
/// </summary>
public interface IBacktestRunner
{
    /// <summary>
    /// Executes the backtest and returns the final account and trade history.
    /// </summary>
    /// <param name="input">All required data and configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BacktestResult> RunAsync(BacktestInput input, CancellationToken cancellationToken);
}
