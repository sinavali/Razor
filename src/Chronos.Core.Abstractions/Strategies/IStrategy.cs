using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Strategies;

/// <summary>
/// Lifecycle contract for a trading strategy.
/// </summary>
public interface IStrategy
{
    /// <summary>Called once before any data is processed. Receives immutable configuration.</summary>
    Task OnConfigureAsync(StrategySpecification spec);
    /// <summary>Called at the start of a run. The indicator registry is ready.</summary>
    Task OnStartAsync(IIndicatorRegistry indicators);

    /// <summary>
    /// Principle 2: Called for every tick in chronological order. 
    /// Must be purely synchronous to guarantee determinism in backtesting and live.
    /// </summary>
    void OnTick(Tick tick);

    /// <summary>
    /// Optional: Called for every tick in live mode ONLY when asynchronous operations 
    /// are truly required (e.g. external IO). This is awaited asynchronously in live.
    /// </summary>
    [Obsolete("Use synchronous OnTick for deterministic logic. This is executed out-of-band in live mode only.")]
    Task OnTickAsync(Tick tick) => Task.CompletedTask;

    /// <summary>Called at the end of a run.</summary>
    Task OnStopAsync();
    /// <summary>Injects chromosome genes into the strategy (for optimisation / live deployment).</summary>
    void InjectGenes(double[] genes);
}
