namespace Razor.Core.Sdk.Hooks;

/// <summary>
/// A candidate solution in the genetic algorithm population.
/// Exposed to hook plugins for inspection and logging.
/// The full <see cref="Chromosome"/> type is defined in Razor.Core.Kernel
/// and is compatible with this contract.
/// </summary>
public sealed record Chromosome
{
    /// <summary>Gene values for this chromosome.</summary>
    public required double[] Genes { get; init; }

    /// <summary>Fitness score (higher = better).</summary>
    public double Fitness { get; init; }

    /// <summary>Generation in which this chromosome was created.</summary>
    public int Generation { get; init; }

    /// <summary>Index within the population.</summary>
    public int IndividualIndex { get; init; }

    /// <summary>Seed used to generate this individual.</summary>
    public int Seed { get; init; }
}
