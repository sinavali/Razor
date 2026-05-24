using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Kernel.Configuration;

/// <summary>
/// Mode for walk‑forward analysis.
/// </summary>
public enum WalkForwardMode
{
    /// <summary>Anchored windows: train start is fixed at the beginning of the data.</summary>
    Anchored,
    /// <summary>Rolling windows: train window slides forward with each step.</summary>
    Rolling
}

/// <summary>
/// Immutable specification for a genetic optimisation run.
/// </summary>
public sealed record OptimizationSpecification
{
    /// <summary>Master seed for reproducibility.</summary>
    public int MasterSeed
    {
        get; init;
    }

    /// <summary>Number of generations to evolve.</summary>
    public int Generations
    {
        get; init;
    }

    /// <summary>Population size.</summary>
    public int PopulationSize
    {
        get; init;
    }

    /// <summary>Number of walk‑forward windows (1 = simple optimisation).</summary>
    public int Windows
    {
        get; init;
    }

    /// <summary>Fraction of data used for training (0.0–1.0).</summary>
    public double TrainSplit
    {
        get; init;
    }

    /// <summary>Base mutation rate.</summary>
    public double MutationRate
    {
        get; init;
    }

    /// <summary>Crossover rate.</summary>
    public double CrossoverRate
    {
        get; init;
    }

    /// <summary>Fraction of population preserved via elitism.</summary>
    public double ElitismPct
    {
        get; init;
    }

    /// <summary>Tournament selection size.</summary>
    public int TournamentSize
    {
        get; init;
    }

    /// <summary>Number of consecutive generations with unchanged best fitness before hyper‑mutation activates.</summary>
    public int StagnationGenerationsBeforeHyper { get; init; } = 3;

    /// <summary>Walk‑forward mode (only used when Windows > 1).</summary>
    public WalkForwardMode WalkForwardMode { get; init; } = WalkForwardMode.Anchored;

    /// <summary>Fitness model used to evaluate chromosome quality.</summary>
    public IFitnessModel? FitnessModel { get; init; }

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

        if (Windows <= 0)
        {
            throw new ConfigurationException("Windows must be 1 or more.");
        }

        if (TrainSplit <= 0 || TrainSplit >= 1)
        {
            throw new ConfigurationException("TrainSplit must be between 0 and 1 (exclusive).");
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
        int windows,
        double trainSplit,
        double mutationRate,
        double crossoverRate,
        double elitismPct,
        int tournamentSize,
        IFitnessModel fitnessModel,
        int stagnationGenerationsBeforeHyper = 3,
        WalkForwardMode walkForwardMode = WalkForwardMode.Anchored)
    {
        var spec = new OptimizationSpecification
        {
            MasterSeed = masterSeed,
            Generations = generations,
            PopulationSize = populationSize,
            Windows = windows,
            TrainSplit = trainSplit,
            MutationRate = mutationRate,
            CrossoverRate = crossoverRate,
            ElitismPct = elitismPct,
            TournamentSize = tournamentSize,
            StagnationGenerationsBeforeHyper = stagnationGenerationsBeforeHyper,
            WalkForwardMode = walkForwardMode,
            FitnessModel = fitnessModel
        };
        spec.Validate();
        return spec;
    }
}
