namespace Chronos.Core.Kernel.Optimization;

/// <summary>
/// Steppable genetic algorithm engine.
/// The host controls generation flow and supplies a fitness evaluator.
/// </summary>
public interface IGeneticOptimizer : IOptimizer
{
    /// <summary>Current generation index (0‑based).</summary>
    int CurrentGeneration { get; }
    /// <summary>Population size.</summary>
    int PopulationSize { get; }
    /// <summary>Whether hyper‑mutation is currently active.</summary>
    bool IsHyperMutation { get; }
    /// <summary>Best solution found so far.</summary>
    Chromosome BestSolution { get; }
    /// <summary>Current population snapshot (sorted).</summary>
    IReadOnlyList<Chromosome> Population { get; }

    /// <summary>Initialises or resets the population for generation 0.</summary>
    void Initialize();

    /// <summary>
    /// Evaluates every unevaluated chromosome using the provided delegate.
    /// </summary>
    /// <param name="evaluator">Function that returns a fitness value (higher = better).</param>
    /// <param name="ct">Cancellation token.</param>
    Task EvaluateAsync(Func<Chromosome, CancellationToken, Task<double>> evaluator, CancellationToken ct);

    /// <summary>
    /// Advances the population by one generation (selection, crossover, mutation).
    /// Call <see cref="EvaluateAsync"/> after each evolution step.
    /// </summary>
    void Evolve();
}
