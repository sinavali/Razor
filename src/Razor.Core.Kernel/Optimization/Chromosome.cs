#pragma warning disable IDE0290 // Reason: Primary constructor not used here for clarity.

using System.Text.Json.Serialization;

namespace Razor.Core.Kernel.Optimization;

/// <summary>
/// A single candidate solution in the genetic algorithm population.
/// </summary>
public sealed class Chromosome
{
    /// <summary>
    /// Fitness value that indicates a chromosome has not yet been evaluated.
    /// </summary>
    public const double NotEvaluated = double.NegativeInfinity;

    /// <summary>Genes array (normalised property values + optional neural weights).</summary>
    [JsonInclude]
    public double[] Genes { get; }

    /// <summary>Fitness value (higher = better). Set by the evaluation function.</summary>
    public double Fitness { get; set; } = NotEvaluated;

    /// <summary>Generation in which this chromosome was created.</summary>
    public int Generation { get; set; }

    /// <summary>Index within the population (0‑based).</summary>
    public int IndividualIndex { get; set; }

    /// <summary>Random seed used to generate this individual (0 = evolved).</summary>
    public int Seed { get; set; }

    /// <summary>Constructs a new Chromosome instance.</summary>
    public Chromosome(int geneCount)
    {
        Genes = new double[geneCount];
    }

    /// <summary>Constructs a Chromosome from its serialised fields (used by JSON deserialisation).</summary>
    [JsonConstructor]
    public Chromosome(double[] genes, double fitness, int generation, int individualIndex, int seed)
    {
        Genes = genes ?? [];
        Fitness = fitness;
        Generation = generation;
        IndividualIndex = individualIndex;
        Seed = seed;
    }

    /// <summary>Deep clones the current chromosome.</summary>
    public Chromosome Clone()
    {
        var clone = new Chromosome(Genes.Length);
        Array.Copy(Genes, clone.Genes, Genes.Length);
        clone.Fitness = Fitness;
        clone.Generation = Generation;
        clone.IndividualIndex = IndividualIndex;
        clone.Seed = Seed;
        return clone;
    }
}

/// <summary>
/// Extension methods for <see cref="Chromosome"/>.
/// </summary>
internal static class ChromosomeExtensions
{
    /// <summary>Copies genes, fitness, and seed from another chromosome.</summary>
    public static void CopyFrom(this Chromosome target, Chromosome source)
    {
        Array.Copy(source.Genes, target.Genes, target.Genes.Length);
        target.Fitness = source.Fitness;
        target.Seed = source.Seed;
    }
}
