using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;
using Chronos.Core.Abstractions.Telemetry;
using Chronos.Core.Kernel.Configuration;
using Chronos.Core.Kernel.Telemetry;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>
/// Immutable input for a single backtest run.
/// </summary>
public sealed record BacktestInput
{
    /// <summary>Pre‑loaded tick streams, one per symbol (may be memory‑mapped).</summary>
    public required IReadOnlyList<Tick>[] TickStreams { get; init; }
    /// <summary>Symbol names in the same order as <see cref="TickStreams"/>.</summary>
    public required string[] Symbols { get; init; }
    /// <summary>The strategy instance to execute.</summary>
    public required IStrategy Strategy { get; init; }
    /// <summary>Immutable strategy configuration.</summary>
    public required StrategySpecification StrategySpecification { get; init; }
    /// <summary>Execution parameters (warmup, latency, etc.).</summary>
    public required ExecutionSpecification ExecutionSpecification { get; init; }
    /// <summary>Exchange‑specific financial calculator (provided by the adapter).</summary>
    public required IMarketCalculator MarketCalculator { get; init; }
    /// <summary>Symbol properties for all requested symbols.</summary>
    public required Dictionary<string, SymbolProperties> SymbolProperties { get; init; }
    /// <summary>Optional pre‑set genes. If null, genes are initialised deterministically.</summary>
    public double[]? Genes { get; init; }
    /// <summary>Optional neural network whose weights are part of the chromosome.</summary>
    public FeedForwardNetwork? NeuralNetwork { get; init; }
    /// <summary>Seed used for deterministic gene initialization when <see cref="Genes"/> is null.</summary>
    public required int GeneInitializationSeed { get; init; }
    /// <summary>Optional progress reporter.</summary>
    public IProgress<BacktestProgress>? Progress { get; init; }
    /// <summary>Optional message bus for event publication.</summary>
    public IMessageBus? MessageBus { get; init; }
    /// <summary>Optional metrics recorder.</summary>
    public IChronosMetrics? Metrics { get; init; }

    /// <summary>Validates the inputs prior to execution.</summary>
    public void Validate()
    {
        if (Genes is null && GeneInitializationSeed == 0)
            throw new ConfigurationException("GeneInitializationSeed must be non-zero when Genes is not pre-supplied.");

        if (StrategySpecification.FrictionModel == null)
            throw new ConfigurationException("FrictionModel is required for backtesting contexts.");
    }
}
