// -----------------------------------------------------------------------------
// <copyright file="IKernelService.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Kernel;

using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Chronos.Core.Engine.Management.Tasks;
using Chronos.Core.Kernel.Backtesting;
using Chronos.Core.Kernel.Optimization;
using ChromosomeKernel = Chronos.Core.Kernel.Optimization.Chromosome;

/// <summary>
/// Facade for the Chronos Kernel, providing methods for backtest, live, and optimisation execution.
/// </summary>
internal interface IKernelService
{
    /// <summary>Starts a backtest using the given adapter and strategy.</summary>
    Task<string> StartBacktestAsync(IAdapterCapability adapter, IStrategyCapability strategy, BacktestConfiguration config, CancellationToken cancellationToken);

    /// <summary>Gets the result of a completed backtest.</summary>
    Task<BacktestResult> GetBacktestResultAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Starts a live trading session.</summary>
    Task<string> StartLiveAsync(LiveInput input, CancellationToken cancellationToken);

    /// <summary>Gets the current live state.</summary>
    Task<LiveState> GetLiveStateAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Stops a live trading session.</summary>
    Task StopLiveAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Pauses a live trading session (no new orders, but positions remain open).</summary>
    Task PauseLiveAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Resumes a paused live trading session.</summary>
    Task ResumeLiveAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Injects a gene array into the live strategy.</summary>
    Task InjectGenesAsync(string taskId, double[] genes, CancellationToken cancellationToken);

    /// <summary>Starts an optimisation run.</summary>
    Task<string> StartOptimizationAsync(OptimizationInput input, CancellationToken cancellationToken);

    /// <summary>Gets the best chromosome from a completed optimisation.</summary>
    Task<ChromosomeKernel> GetOptimizationResultAsync(string taskId, CancellationToken cancellationToken);
}

/// <summary>Input for starting a live trading session.</summary>
internal sealed record LiveInput
{
    public required string AdapterName { get; init; }
    public required string StrategyName { get; init; }
    public required object StrategyConfig { get; init; }
    public required int MagicNumber { get; init; }
    public required double Leverage { get; init; }
    public required double InitialBalance { get; init; }
    public required string[] Symbols { get; init; }
    public required int OrderGuardTimeoutSeconds { get; init; }
    public required double StopOutLevel { get; init; }
    public required int MaxOpenPositions { get; init; }
    public required double[] Genes { get; init; }
    public required string NeuralNetworkName { get; init; }  // optional, empty means none
}

/// <summary>Input for starting an optimisation run.</summary>
internal sealed record OptimizationInput
{
    public required string AdapterName { get; init; }
    public required string StrategyName { get; init; }
    public required object StrategyConfig { get; init; }
    public required double Leverage { get; init; }
    public required double InitialBalance { get; init; }
    public required string[] Symbols { get; init; }
    public required int MasterSeed { get; init; }
    public required int Generations { get; init; }
    public required int PopulationSize { get; init; }
    public required double MutationRate { get; init; }
    public required double CrossoverRate { get; init; }
    public required double ElitismPct { get; init; }
    public required int TournamentSize { get; init; }
    public required int StagnationGenerationsBeforeHyper { get; init; }
    public required int MaxParallelThreads { get; init; }
    public required string NeuralNetworkName { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required string[] Timeframes { get; init; }
}

/// <summary>Snapshot of live trading state.</summary>
internal sealed record LiveState
{
    public bool IsLive { get; init; }
    public double Equity { get; init; }
    public double Balance { get; init; }
    public double Drawdown { get; init; }
    public IReadOnlyList<object> Positions { get; init; } = Array.Empty<object>();
    public IReadOnlyList<object> Orders { get; init; } = Array.Empty<object>();
}
