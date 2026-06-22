// -----------------------------------------------------------------------------
// <copyright file="StateTypes.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Core;

/// <summary>
/// Persistent state for a live trading task.
/// </summary>
internal sealed record LiveState
{
    /// <summary>Gets the task identifier.</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Gets the configuration object used to start the task.</summary>
    public object Config { get; init; } = new object();

    /// <summary>Gets the gene array currently injected into the strategy.</summary>
    public double[] Genes { get; init; } = Array.Empty<double>();

    /// <summary>Gets the timestamp of the last tick received, or null if none.</summary>
    public DateTime? LastTickTime { get; init; }

    /// <summary>Gets the start time of the task.</summary>
    public DateTime StartTime { get; init; }

    /// <summary>Gets the name of the active strategy.</summary>
    public string StrategyName { get; init; } = string.Empty;

    /// <summary>Gets the name of the active adapter.</summary>
    public string AdapterName { get; init; } = string.Empty;
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

    /// <summary>Gets the current population snapshot (placeholder).</summary>
    public object Population { get; init; } = new object();

    /// <summary>Gets the current generation number.</summary>
    public int CurrentGeneration { get; init; }

    /// <summary>Gets the best fitness found so far.</summary>
    public double BestFitness { get; init; }

    /// <summary>Gets the start time of the task.</summary>
    public DateTime StartTime { get; init; }
}
