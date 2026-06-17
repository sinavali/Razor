namespace Chronos.Core.Abstractions.Hooks;

/// <summary>
/// Context provided to optimization hook callbacks.
/// </summary>
public interface IOptimizationContext : IHookContext
{
    /// <summary>Zero‑based index of the current generation.</summary>
    int CurrentGeneration { get; }

    /// <summary>Total number of generations configured.</summary>
    int TotalGenerations { get; }

    /// <summary>Population size.</summary>
    int PopulationSize { get; }

    /// <summary>Best fitness observed so far.</summary>
    double BestFitness { get; }

    /// <summary>Whether hyper‑mutation is currently active.</summary>
    bool IsHyperMutation { get; }
}
