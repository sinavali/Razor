namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Lifecycle contract for a trading strategy.
/// </summary>
public interface IStrategy
{
    /// <summary>Called once before any data is processed. Receives immutable configuration.</summary>
    Task OnConfigureAsync(StrategySpecification spec);

    /// <summary>Called at the start of a run. The indicator registry is ready.</summary>
    Task OnStartAsync(IIndicatorRegistry indicators);

    /// <summary>Called for every tick in chronological order.</summary>
    Task OnTickAsync(Chronos.Abstractions.Shared.Tick tick);

    /// <summary>Called at the end of a run.</summary>
    Task OnStopAsync();

    /// <summary>Injects chromosome genes into the strategy (for optimisation / live deployment).</summary>
    void InjectGenes(double[] genes);
}