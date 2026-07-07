using Chronos.Core.Abstractions.Hooks;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Optimization hook registration points.
/// </summary>
public sealed class OptimizationHooks : IOptimizationHooks
{
    /// <inheritdoc/>
    public IActionRegistration OnStart { get; } = new ActionRegistration();

    /// <inheritdoc/>
    public IActionRegistration<int> OnGenerationStart { get; } = new ActionRegistration<int>();

    /// <inheritdoc/>
    public IFilterRegistration<Chromosome> OnChromosomeCreated { get; } = new FilterRegistration<Chromosome>();

    /// <inheritdoc/>
    public IActionRegistration<(Chromosome Chromosome, double Fitness)> OnChromosomeEvaluated { get; }
        = new ActionRegistration<(Chromosome, double)>();

    /// <inheritdoc/>
    public IActionRegistration<(Chromosome Parent1, Chromosome Parent2)> OnSelectionApplied { get; }
        = new ActionRegistration<(Chromosome, Chromosome)>();

    /// <inheritdoc/>
    public IActionRegistration<Chromosome> OnCrossoverApplied { get; } = new ActionRegistration<Chromosome>();

    /// <inheritdoc/>
    public IActionRegistration<Chromosome> OnMutationApplied { get; } = new ActionRegistration<Chromosome>();

    /// <inheritdoc/>
    public IActionRegistration<(int Generation, double BestFitness, bool IsHyperMutation)> OnGenerationCompleted { get; }
        = new ActionRegistration<(int, double, bool)>();

    /// <inheritdoc/>
    public IActionRegistration<int> OnStagnationDetected { get; } = new ActionRegistration<int>();

    /// <inheritdoc/>
    public IActionRegistration<Chromosome> OnCompleted { get; } = new ActionRegistration<Chromosome>();

    /// <inheritdoc/>
    public IActionRegistration<IFitnessEvaluationContext> OnFitnessEvaluation { get; }
        = new ActionRegistration<IFitnessEvaluationContext>();

    /// <summary>
    /// Clears all registered callbacks from all hook points.
    /// </summary>
    public void ClearAll()
    {
        ((ActionRegistration)OnStart).Clear();
        ((ActionRegistration<int>)OnGenerationStart).Clear();
        ((FilterRegistration<Chromosome>)OnChromosomeCreated).Clear();
        ((ActionRegistration<(Chromosome, double)>)OnChromosomeEvaluated).Clear();
        ((ActionRegistration<(Chromosome, Chromosome)>)OnSelectionApplied).Clear();
        ((ActionRegistration<Chromosome>)OnCrossoverApplied).Clear();
        ((ActionRegistration<Chromosome>)OnMutationApplied).Clear();
        ((ActionRegistration<(int, double, bool)>)OnGenerationCompleted).Clear();
        ((ActionRegistration<int>)OnStagnationDetected).Clear();
        ((ActionRegistration<Chromosome>)OnCompleted).Clear();
        ((ActionRegistration<IFitnessEvaluationContext>)OnFitnessEvaluation).Clear();
    }
}
