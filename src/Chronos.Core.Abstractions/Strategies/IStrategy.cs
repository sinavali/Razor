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
    /// Called for every tick in chronological order.
    /// Must be purely synchronous to guarantee determinism.
    /// </summary>
    void OnTick(Tick tick);

    /// <summary>
    /// Called for every tick in live mode when asynchronous operations are truly required.
    /// Default no‑op; override only if you need external async calls.
    /// </summary>
    Task OnTickAsync(Tick tick) => Task.CompletedTask;

    /// <summary>Called at the end of a run.</summary>
    Task OnStopAsync();

    /// <summary>Injects chromosome genes into the strategy (for optimisation / live deployment).</summary>
    void InjectGenes(double[] genes);
}
