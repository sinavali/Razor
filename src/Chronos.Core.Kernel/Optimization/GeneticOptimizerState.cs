namespace Chronos.Core.Kernel.Optimization;

/// <summary>Immutable snapshot of the optimiser state for pause/resume.</summary>
public sealed record GeneticOptimizerState
{
    /// <summary>Population snapshot (deep copy).</summary>
    public required Chromosome[] Population
    {
        get; init;
    }

    /// <summary>Current generation index.</summary>
    public int CurrentGeneration
    {
        get; init;
    }

    /// <summary>Whether the current population has been evaluated.</summary>
    public bool Evaluated
    {
        get; init;
    }

    /// <summary>Best fitness observed across all generations.</summary>
    public double BestOverallFitness
    {
        get; init;
    } = double.MinValue;

    /// <summary>Consecutive generations without improvement.</summary>
    public int StagnationCount
    {
        get; init;
    }

    /// <summary>Whether hyper‑mutation is active.</summary>
    public bool HyperMutation
    {
        get; init;
    }
}
