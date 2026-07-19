// -----------------------------------------------------------------------------
// <copyright file="StateTypes.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

using Razor.Core.Kernel.Optimization;

namespace Razor.Core.Engine.Core;

/// <summary>
/// Persistent state for a live trading task.
/// </summary>
internal sealed record LiveState
{
    /// <summary>Gets the task identifier.</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Gets the configuration object used to start the task (for extensibility).</summary>
    public object Config { get; init; } = new object();

    /// <summary>Gets the name of the active adapter.</summary>
    public string AdapterName { get; init; } = string.Empty;

    /// <summary>Gets the name of the active strategy.</summary>
    public string StrategyName { get; init; } = string.Empty;

    /// <summary>Gets the base account currency (e.g. USD).</summary>
    public string AccountCurrency { get; init; } = "USD";

    /// <summary>Gets the magic number for order tagging.</summary>
    public int MagicNumber { get; init; }

    /// <summary>Gets the account leverage.</summary>
    public double Leverage { get; init; }

    /// <summary>Gets the initial account balance.</summary>
    public double InitialBalance { get; init; }

    /// <summary>Gets the symbols being traded.</summary>
    public string[] Symbols { get; init; } = Array.Empty<string>();

    /// <summary>Gets the order guard timeout in seconds.</summary>
    public int OrderGuardTimeoutSeconds { get; init; } = 5;

    /// <summary>Gets the stop‑out level.</summary>
    public double StopOutLevel { get; init; } = 0.5;

    /// <summary>Gets the maximum open positions allowed.</summary>
    public int MaxOpenPositions { get; init; } = 5;

    /// <summary>Gets the name of the neural network model (empty if none).</summary>
    public string NeuralNetworkName { get; init; } = string.Empty;

    /// <summary>Gets the gene array currently injected into the strategy.</summary>
    public double[] Genes { get; init; } = Array.Empty<double>();

    /// <summary>Gets the timestamp of the last tick received, or null if none.</summary>
    public DateTime? LastTickTime { get; init; }

    /// <summary>Gets the start time of the task.</summary>
    public DateTime StartTime { get; init; }

    /// <summary>Gets the ISO currency code for the trading account.</summary>
    public string AccountCurrency { get; init; } = string.Empty;
}

/// <summary>
/// Persistent state for an optimisation task.
/// </summary>
internal sealed record OptimizationState
{
    /// <summary>Gets the task identifier.</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Gets the configuration object used to start the task.</summary>
    public object Config { get; init; } = new object();

    /// <summary>Gets the current population snapshot, or null if not yet initialised.</summary>
    public GeneticOptimizerState? Population { get; init; }

    /// <summary>Gets the current generation number.</summary>
    public int CurrentGeneration { get; init; }

    /// <summary>Gets the best fitness found so far.</summary>
    public double BestFitness { get; init; }

    /// <summary>Gets the start time of the task.</summary>
    public DateTime StartTime { get; init; }
}
