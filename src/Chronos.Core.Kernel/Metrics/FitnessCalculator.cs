using Chronos.Core.Kernel.Backtesting;

namespace Chronos.Core.Kernel.Metrics;

/// <summary>
/// Static helper for computing a default fitness from a backtest result.
/// (Fitness is normally provided by hook plugins; this is a fallback.)
/// </summary>
public static class FitnessCalculator
{
    /// <summary>
    /// Computes a default fitness score (net profit). For advanced fitness,
    /// use the <c>optimization.chromosome.evaluated</c> hook.
    /// </summary>
    public static double Calculate(BacktestResult result, double initialBalance)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Balance - initialBalance;
    }
}
