namespace Razor.Core.Kernel.Optimization;

/// <summary>Progress information emitted during optimisation.</summary>
public sealed record OptimizationProgress(int Generation, double BestFitness, bool IsHyperMutation);
