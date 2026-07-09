using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.NeuralNetwork;

namespace Razor.Core.Sdk.Slots.Strategy;

/// <summary>
/// Capability interface for trading strategies.
/// Implementations are discovered in the <c>Strategies/</c> directory.
/// </summary>
public interface IStrategyCapability
{
    // ── lifecycle ─────────────────────────────────────────────────

    /// <summary>Called once before any data is processed. Receives immutable configuration.</summary>
    Task OnConfigureAsync(StrategySpecification spec);

    /// <summary>Called at the start of a run. The indicator registry is ready.</summary>
    Task OnStartAsync(IIndicatorRegistry indicators);

    /// <summary>
    /// Called for every tick in chronological order.
    /// Must be purely synchronous to guarantee determinism in backtesting and live.
    /// </summary>
    /// <param name="symbol">The symbol this tick belongs to (e.g. "EURUSD").</param>
    /// <param name="tick">The tick data.</param>
    void OnTick(string symbol, Tick tick);

    /// <summary>Called at the end of a run.</summary>
    Task OnStopAsync();

    // ── gene support ──────────────────────────────────────────────

    /// <summary>
    /// Total number of genes in the chromosome for this strategy.
    /// Includes both property genes and neural network parameters.
    /// </summary>
    int TotalGeneCount { get; }

    /// <summary>Injects a chromosome's gene values into the strategy.</summary>
    void InjectGenes(double[] genes);

    /// <summary>Exports the current gene values from the strategy.</summary>
    double[] ExportGenes();

    // ── neural network ────────────────────────────────────────────

    /// <summary>
    /// Whether this strategy requires a neural network model to function.
    /// If true, the engine must activate an <see cref="INeuralNetworkModel"/>.
    /// </summary>
    bool RequiresNeuralNetwork { get; }

    /// <summary>The neural network model, set by the engine before initialization.</summary>
    INeuralNetworkModel? NeuralNetwork { get; set; }
}
