#pragma warning disable IDE0290 // Reason: Primary constructor not used here to keep explicit constructor for clarity.
namespace Chronos.Kernel.Optimization;

/// <summary>
/// A single candidate solution in the genetic algorithm population.
/// </summary>
public sealed class Chromosome
{
    /// <summary>Genes array (normalised property values + optional neural weights).</summary>
    public double[] Genes
    {
        get;
    }

    /// <summary>Fitness value (higher = better). Set by the evaluation function.</summary>
    public double Fitness { get; set; } = double.MinValue;

    /// <summary>Generation in which this chromosome was created.</summary>
    public int Generation
    {
        get; set;
    }

    /// <summary>Index within the population (0‑based).</summary>
    public int IndividualIndex
    {
        get; set;
    }

    /// <summary>Random seed used to generate this individual (0 = evolved).</summary>
    public int Seed
    {
        get; set;
    }

    /// <summary>Creates a new chromosome with the given gene count.</summary>
    public Chromosome(int geneCount)
    {
        Genes = new double[geneCount];
    }

    /// <summary>Deep copy.</summary>
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

// Extension for copying chromosomes
internal static class ChromosomeExtensions
{
    public static void CopyFrom(this Chromosome target, Chromosome source)
    {
        Array.Copy(source.Genes, target.Genes, target.Genes.Length);
        target.Fitness = source.Fitness;
        target.Seed = source.Seed;
    }
}