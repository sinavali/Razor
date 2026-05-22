using Chronos.Abstractions.Strategies;
using Chronos.Kernel.Backtesting;

namespace Chronos.Kernel.Metrics;

/// <summary>
/// Static helper for computing fitness from a backtest result.
/// </summary>
public static class FitnessCalculator
{
    /// <summary>
    /// Calculates fitness using the provided <see cref="IFitnessModel"/>.
    /// If the model is null, returns net profit.
    /// </summary>
    public static double Calculate(BacktestResult result, IFitnessModel? fitnessModel, double initialBalance)
    {
        ArgumentNullException.ThrowIfNull(result);

        return fitnessModel?.Evaluate(
                   result.Balance,
                   initialBalance,
                   result.Drawdown,
                   result.DailyDrawdown,
                   result.TotalTrades,
                   result.History)
               ?? result.Balance - initialBalance;
    }
}