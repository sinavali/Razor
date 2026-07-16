using Razor.Core.Kernel.Backtesting;

namespace Razor.Core.Kernel.Metrics;

/// <summary>
/// Calculates performance metrics from a completed backtest.
/// </summary>
public interface IMetricsCalculator
{
    /// <summary>
    /// Produces a <see cref="SummaryMetrics"/> record from raw backtest results.
    /// </summary>
    /// <param name="result">The backtest outcome.</param>
    /// <param name="initialBalance">Starting account balance.</param>
    /// <param name="startDate">Start of the data window (optional, for annualisation).</param>
    /// <param name="endDate">End of the data window (optional, for annualisation).</param>
    SummaryMetrics Calculate(BacktestResult result, double initialBalance,
        DateTime startDate = default, DateTime endDate = default);
}
