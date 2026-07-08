using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Kernel.Configuration;

/// <summary>
/// Immutable specification for a genetic optimisation run.
/// Fitness evaluation is performed externally via hooks; no built‑in fitness model.
/// </summary>
public sealed record OptimizationSpecification
{
    /// <summary>Master seed for reproducibility (must be ≥ 0).</summary>
    public required int MasterSeed { get; init; }

    /// <summary>Number of generations to evolve.</summary>
    public required int Generations { get; init; }

    /// <summary>Population size (must be ≥ 4).</summary>
    public required int PopulationSize { get; init; }

    /// <summary>Base mutation rate (0‑1).</summary>
    public required double MutationRate { get; init; }

    /// <summary>Crossover rate (0‑1).</summary>
    public required double CrossoverRate { get; init; }

    /// <summary>Fraction of population preserved via elitism (0‑1).</summary>
    public required double ElitismPct { get; init; }

    /// <summary>Tournament selection size (≥ 2).</summary>
    public required int TournamentSize { get; init; }

    /// <summary>Number of consecutive generations with unchanged best fitness before hyper‑mutation activates.</summary>
    public int StagnationGenerationsBeforeHyper { get; init; } = 3;

    /// <summary>
    /// Maximum parallel threads for chromosome evaluation.
    /// 0 = auto (Environment.ProcessorCount - 1). Must be ≥ 0.
    /// </summary>
    public int MaxParallelThreads { get; init; }

    /// <summary>Validates this specification.</summary>
    public void Validate()
    {
        if (MasterSeed < 0)
        {
            throw new ConfigurationException("MasterSeed must be non‑negative.");
        }

        if (Generations <= 0)
        {
            throw new ConfigurationException("Generations must be positive.");
        }

        if (PopulationSize < 4)
        {
            throw new ConfigurationException("PopulationSize must be at least 4.");
        }

        if (MutationRate < 0 || MutationRate > 1)
        {
            throw new ConfigurationException("MutationRate must be between 0 and 1.");
        }

        if (CrossoverRate < 0 || CrossoverRate > 1)
        {
            throw new ConfigurationException("CrossoverRate must be between 0 and 1.");
        }

        if (ElitismPct < 0 || ElitismPct > 1)
        {
            throw new ConfigurationException("ElitismPct must be between 0 and 1.");
        }

        if (TournamentSize < 2)
        {
            throw new ConfigurationException("TournamentSize must be at least 2.");
        }

        if (StagnationGenerationsBeforeHyper < 1)
        {
            throw new ConfigurationException("StagnationGenerationsBeforeHyper must be at least 1.");
        }

        if (MaxParallelThreads < 0)
        {
            throw new ConfigurationException("MaxParallelThreads must be ≥ 0.");
        }
    }

    /// <summary>Creates a validated instance.</summary>
    public static OptimizationSpecification CreateValidated(
        int masterSeed,
        int generations,
        int populationSize,
        double mutationRate,
        double crossoverRate,
        double elitismPct,
        int tournamentSize,
        int stagnationGenerationsBeforeHyper = 3,
        int maxParallelThreads = 0)
    {
        var spec = new OptimizationSpecification
        {
            MasterSeed = masterSeed,
            Generations = generations,
            PopulationSize = populationSize,
            MutationRate = mutationRate,
            CrossoverRate = crossoverRate,
            ElitismPct = elitismPct,
            TournamentSize = tournamentSize,
            StagnationGenerationsBeforeHyper = stagnationGenerationsBeforeHyper,
            MaxParallelThreads = maxParallelThreads
        };
        spec.Validate();
        return spec;
    }
}
