namespace Chronos.Core.Kernel.Optimization;

/// <summary>Immutable snapshot of the optimiser state for pause/resume.</summary>
public sealed record GeneticOptimizerState
{
    /// <summary>Population snapshot (deep copy).</summary>
    public required Chromosome[] Population { get; init; }

    /// <summary>Current generation index.</summary>
    public int CurrentGeneration { get; init; }

    /// <summary>Whether the current population has been evaluated.</summary>
    public bool Evaluated { get; init; }

    /// <summary>Best fitness observed across all generations.</summary>
    public double BestOverallFitness { get; init; } = Chromosome.NotEvaluated;

    /// <summary>Consecutive generations without improvement.</summary>
    public int StagnationCount { get; init; }

    /// <summary>Whether hyper‑mutation is active.</summary>
    public bool HyperMutation { get; init; }

    /// <summary>
    /// Serialised state of the active neural network model, if any.
    /// <c>null</c> if no neural network is in use or if serialisation is not supported.
    /// </summary>
    public byte[]? NeuralNetworkState { get; init; }
}
