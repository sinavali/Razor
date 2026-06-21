namespace Chronos.Core.Abstractions.Hooks;

/// <summary>Hook registration points for the optimization pipeline.</summary>
public interface IOptimizationHooks
{
    /// <summary>Fires when an optimization run starts.</summary>
    IActionRegistration OnStart { get; }

    /// <summary>Action at the start of each generation.</summary>
    IActionRegistration<int> OnGenerationStart { get; }

    /// <summary>Filter a chromosome immediately after creation.</summary>
    IFilterRegistration<Chromosome> OnChromosomeCreated { get; }

    /// <summary>Action after a chromosome's fitness is evaluated.</summary>
    IActionRegistration<(Chromosome Chromosome, double Fitness)> OnChromosomeEvaluated { get; }

    /// <summary>Action when parents are selected for crossover.</summary>
    IActionRegistration<(Chromosome Parent1, Chromosome Parent2)> OnSelectionApplied { get; }

    /// <summary>Action when a child chromosome is produced via crossover.</summary>
    IActionRegistration<Chromosome> OnCrossoverApplied { get; }

    /// <summary>Action when a chromosome is mutated.</summary>
    IActionRegistration<Chromosome> OnMutationApplied { get; }

    /// <summary>Action at the end of each generation.</summary>
    IActionRegistration<(int Generation, double BestFitness, bool IsHyperMutation)> OnGenerationCompleted { get; }

    /// <summary>Action when stagnation is detected (hyper‑mutation may activate).</summary>
    IActionRegistration<int> OnStagnationDetected { get; }

    /// <summary>Fires when the optimization completes.</summary>
    IActionRegistration<Chromosome> OnCompleted { get; }

    /// <summary>
    /// Hook for calculating fitness of a chromosome.
    /// Plugins can set <c>context.Fitness</c>; the first non‑NaN value wins.
    /// Fires before <see cref="OnChromosomeEvaluated"/>.
    /// </summary>
    IActionRegistration<IFitnessEvaluationContext> OnFitnessEvaluation { get; }
}
