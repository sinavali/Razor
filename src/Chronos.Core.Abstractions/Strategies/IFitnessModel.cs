namespace Chronos.Core.Abstractions.Strategies;

/// <summary>
/// User‑supplied fitness function. Receives backtest summary statistics and returns a scalar
/// where higher values indicate better performance.
/// </summary>
public interface IFitnessModel
{
    /// <summary>Evaluates fitness from a completed backtest.</summary>
    double Evaluate(double finalBalance, double initialBalance, double maxDrawdown,
        double maxDailyDrawdown, int totalTrades,
        IReadOnlyList<Chronos.Core.Abstractions.Shared.Position> history);
}
