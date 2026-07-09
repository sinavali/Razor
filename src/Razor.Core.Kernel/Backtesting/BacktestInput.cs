using Razor.Core.Kernel.Configuration;
using Razor.Core.Kernel.Messaging;
using Razor.Core.Kernel.Telemetry;
using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.NeuralNetwork;
using Razor.Core.Sdk.Slots.Strategy;

namespace Razor.Core.Kernel.Backtesting;

/// <summary>
/// Immutable input for a single deterministic backtest run.
/// </summary>
public sealed record BacktestInput
{
    /// <summary>Pre‑loaded tick streams, one per symbol (may be memory‑mapped).</summary>
    public required IReadOnlyList<Tick>[] TickStreams { get; init; }

    /// <summary>Symbol names in the same order as <see cref="TickStreams"/>.</summary>
    public required string[] Symbols { get; init; }

    /// <summary>The strategy instance to execute.</summary>
    public required IStrategyCapability Strategy { get; init; }

    /// <summary>Immutable strategy configuration.</summary>
    public required StrategySpecification StrategySpecification { get; init; }

    /// <summary>Execution parameters (warmup, latency, etc.).</summary>
    public required ExecutionSpecification ExecutionSpecification { get; init; }

    /// <summary>Exchange‑specific financial calculator (provided by the adapter).</summary>
    public required IMarketCalculator MarketCalculator { get; init; }

    /// <summary>Symbol properties for all requested symbols.</summary>
    public required Dictionary<string, SymbolProperties> SymbolProperties { get; init; }

    /// <summary>Optional pre‑set genes. If null, the strategy's default genes are used.</summary>
    public double[]? Genes { get; init; }

    /// <summary>Optional neural network whose weights are part of the chromosome.</summary>
    public INeuralNetworkModel? NeuralNetwork { get; init; }

    /// <summary>
    /// Seed used for deterministic gene initialization when <see cref="Genes"/> is null.
    /// Use <see langword="null"/> to keep the strategy's current default values.
    /// </summary>
    public int? GeneInitializationSeed { get; init; }

    /// <summary>Optional progress reporter.</summary>
    public IProgress<BacktestProgress>? Progress { get; init; }

    /// <summary>Optional message bus for event publication.</summary>
    public IMessageBus? MessageBus { get; init; }

    /// <summary>Optional metrics recorder.</summary>
    public ICoreMetrics? Metrics { get; init; }

    /// <summary>
    /// Optional hook registry for invoking backtest pipeline hooks.
    /// </summary>
    public IHookRegistry? HookRegistry { get; init; }

    /// <summary>
    /// Optional currency converter for cross‑currency PnL and margin calculations.
    /// </summary>
    public ICurrencyConverter? CurrencyConverter { get; init; }

    /// <summary>
    /// Account base currency (e.g., "USD"). Used with <see cref="CurrencyConverter"/>.
    /// </summary>
    public string? AccountCurrency { get; init; }
}
