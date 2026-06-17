using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Kernel.Configuration;

/// <summary>
/// Immutable specification for a genetic optimisation run.
/// </summary>
public sealed record OptimizationSpecification
{
    /// <summary>Master seed for reproducibility.</summary>
    public required int MasterSeed { get; init; }

    /// <summary>Number of generations to evolve.</summary>
    public required int Generations { get; init; }

    /// <summary>Population size.</summary>
    public required int PopulationSize { get; init; }

    /// <summary>Base mutation rate.</summary>
    public required double MutationRate { get; init; }

    /// <summary>Crossover rate.</summary>
    public required double CrossoverRate { get; init; }

    /// <summary>Fraction of population preserved via elitism.</summary>
    public required double ElitismPct { get; init; }

    /// <summary>Tournament selection size.</summary>
    public required int TournamentSize { get; init; }

    /// <summary>Number of consecutive generations with unchanged best fitness before hyper‑mutation activates.</summary>
    public int StagnationGenerationsBeforeHyper { get; init; } = 3;

    /// <summary>Fitness model used to evaluate chromosome quality.</summary>
    public required IFitnessModel FitnessModel { get; init; }

    /// <summary>Validates this specification.</summary>
    public void Validate()
    {
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

        if (FitnessModel == null)
        {
            throw new ConfigurationException("FitnessModel is required for optimisation.");
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
        IFitnessModel fitnessModel,
        int stagnationGenerationsBeforeHyper = 3)
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
            FitnessModel = fitnessModel
        };
        spec.Validate();
        return spec;
    }
}
