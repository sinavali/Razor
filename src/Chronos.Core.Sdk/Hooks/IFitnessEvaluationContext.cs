namespace Chronos.Core.Sdk.Hooks;

/// <summary>
/// Context provided to the <c>optimization.fitness.evaluate</c> hook.
/// Plugins set <see cref="Fitness"/> to override the default fitness value.
/// </summary>
public interface IFitnessEvaluationContext : IHookContext
{
    /// <summary>The chromosome being evaluated.</summary>
    Chromosome Chromosome { get; }

    /// <summary>The fitness value. Plugins can set this to a non‑NaN value to override.</summary>
    double Fitness { get; set; }
}
