using Razor.Core.Kernel.Clock;
using Razor.Core.Sdk.Hooks;

namespace Razor.Core.Kernel.Hooks;

/// <summary>
/// Context provided to optimization hook callbacks.
/// </summary>
public sealed class OptimizationContext : IOptimizationContext
{
    /// <inheritdoc/>
    public int CurrentGeneration { get; }

    /// <inheritdoc/>
    public int TotalGenerations { get; }

    /// <inheritdoc/>
    public int PopulationSize { get; }

    /// <inheritdoc/>
    public double BestFitness { get; }

    /// <inheritdoc/>
    public bool IsHyperMutation { get; }

    /// <inheritdoc/>
    public string HookName { get; }

    /// <inheritdoc/>
    public DateTime UtcNow { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new optimization context.
    /// </summary>
    public OptimizationContext(
        SystemClock clock,
        int currentGeneration,
        int totalGenerations,
        int populationSize,
        double bestFitness,
        bool isHyperMutation,
        string hookName = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clock);
        CurrentGeneration = currentGeneration;
        TotalGenerations = totalGenerations;
        PopulationSize = populationSize;
        BestFitness = bestFitness;
        IsHyperMutation = isHyperMutation;
        HookName = hookName;
        UtcNow = clock.GetUtcNow();
        CancellationToken = cancellationToken;
    }
}
