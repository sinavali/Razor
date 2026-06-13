using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;
using Chronos.Core.Abstractions.Telemetry;
using Chronos.Core.Kernel.Telemetry;
using System.Diagnostics;

namespace Chronos.Core.Kernel.Optimization;

/// <summary>
/// Single‑population genetic algorithm with elitism, tournament selection,
/// crossover, mutation, and stagnation‑triggered hyper‑mutation.
/// State is fully serialisable via <see cref="SaveState"/> and <see cref="LoadState"/>.
/// </summary>
public sealed class GeneticOptimizer : IGeneticOptimizer
{
    private readonly IReadOnlyList<GeneAttribute> _schema;
    private readonly int _populationSize;
    private readonly int _masterSeed;
    private readonly ChronosRandom _mainRng;
    private readonly IChronosMetrics _metrics;

    private Chromosome[] _population;
    private int _currentGeneration;
    private bool _evaluated;
    private readonly double _mutationRate;
    private readonly double _crossoverRate;
    private readonly double _elitismPct;
    private readonly int _tournamentSize;
    private double _bestOverallFitness = Chromosome.NotEvaluated;
    private int _stagnationCount;
    private bool _hyperMutation;
    private readonly int _stagnationGenerationsBeforeHyper;
    private const double RelativeFitnessTolerance = 1e-6;   // K‑P1‑4: relative tolerance for stagnation detection

    /// <inheritdoc/>
    public int CurrentGeneration => _currentGeneration;
    /// <inheritdoc/>
    public int PopulationSize => _populationSize;
    /// <inheritdoc/>
    public bool IsHyperMutation => _hyperMutation;
    /// <inheritdoc/>
    public Chromosome BestSolution { get { Sort(); return _population[0]; } }
    /// <inheritdoc/>
    public IReadOnlyList<Chromosome> Population { get { Sort(); return _population; } }
    /// <summary>Configures threading limits for parallel evaluations.</summary>
    public int MaxDegreeOfParallelism { get; set; }

    /// <summary>Initializes a new optimizer instance.</summary>
    public GeneticOptimizer(
        IReadOnlyList<GeneAttribute> schema,
        IChronosMetrics metrics,
        int populationSize = 100,
        int masterSeed = 0,
        double mutationRate = 0.1,
        double crossoverRate = 0.5,
        double elitismPct = 0.05,
        int tournamentSize = 3,
        int stagnationGenerationsBeforeHyper = 3,
        int maxDegreeOfParallelism = 0)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _populationSize = Math.Max(4, populationSize);
        _masterSeed = masterSeed;
        _mutationRate = mutationRate;
        _crossoverRate = crossoverRate;
        _elitismPct = elitismPct;
        _tournamentSize = Math.Max(2, tournamentSize);
        _stagnationGenerationsBeforeHyper = stagnationGenerationsBeforeHyper;

        _mainRng = new ChronosRandom(masterSeed);
        _population = new Chromosome[_populationSize];
        for (int i = 0; i < _populationSize; i++)
        {
            _population[i] = new Chromosome(_schema.Count);
        }

        MaxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Math.Max(1, Environment.ProcessorCount - 1);
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        _currentGeneration = 0;
        _hyperMutation = false;
        _stagnationCount = 0;
        _bestOverallFitness = Chromosome.NotEvaluated;

        for (int i = 0; i < _populationSize; i++)
        {
            var c = _population[i];
            c.Generation = 0;
            c.IndividualIndex = i;
            c.Fitness = Chromosome.NotEvaluated;

            int individualSeed = GenerateIndividualSeed(i);
            c.Seed = individualSeed;
            var indRng = new ChronosRandom(individualSeed);

            for (int j = 0; j < _schema.Count; j++)
            {
                var attr = _schema[j];
                c.Genes[j] = GeneInjector.GenerateRandomGene(indRng, attr.Min, attr.Max, attr.Step);
            }
        }
        _evaluated = false;
    }

    /// <inheritdoc/>
    public async Task EvaluateAsync(Func<Chromosome, CancellationToken, Task<double>> evaluator, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        var unevaluated = _population.Where(c => c.Fitness <= Chromosome.NotEvaluated).ToList();
        if (unevaluated.Count == 0)
        {
            return;
        }

        var parallelOptions = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = MaxDegreeOfParallelism };
        var sw = Stopwatch.StartNew();

        await Parallel.ForEachAsync(unevaluated, parallelOptions,
                async (c, innerCt) => { c.Fitness = await evaluator(c, innerCt).ConfigureAwait(false); })
            .ConfigureAwait(false);

        if (_population.Any(c => c.Fitness <= Chromosome.NotEvaluated))
        {
            throw new OptimizationException("One or more chromosomes were not evaluated.");
        }

        sw.Stop();
        _metrics.RecordOptimizationDuration(sw.Elapsed.TotalSeconds);

        _evaluated = true;
        Sort();
    }

    /// <inheritdoc/>
    public void Evolve()
    {
        if (!_evaluated)
        {
            throw new InvalidOperationException("Population must be evaluated before evolving.");
        }

        Sort();
        double genBest = _population[0].Fitness;

        if (genBest > _bestOverallFitness && _bestOverallFitness > double.NegativeInfinity)
        {
            double improvement = genBest - _bestOverallFitness;
            _metrics.RecordGaFitnessImprovement(improvement);
        }

        // K‑P1‑4: relative tolerance for stagnation detection
        double tolerance = RelativeFitnessTolerance * Math.Max(1.0, Math.Abs(genBest));
        if (Math.Abs(genBest - _bestOverallFitness) < tolerance)
        {
            _stagnationCount++;
        }
        else
        {
            _bestOverallFitness = genBest;
            _stagnationCount = 0;
            _hyperMutation = false;
        }

        if (_stagnationCount >= _stagnationGenerationsBeforeHyper && !_hyperMutation)
        {
            _hyperMutation = true;
            _stagnationCount = 0;
        }

        double currentMutRate = _hyperMutation ? Math.Min(1.0, _mutationRate * 3) : _mutationRate;
        int elitismCount = Math.Max(1, (int)(_populationSize * _elitismPct));

        var nextPop = new Chromosome[_populationSize];
        for (int i = 0; i < _populationSize; i++)
        {
            nextPop[i] = new Chromosome(_schema.Count);
        }

        for (int i = 0; i < elitismCount; i++)
        {
            nextPop[i].CopyFrom(_population[i]);
            nextPop[i].Generation = _currentGeneration + 1;
            nextPop[i].IndividualIndex = i;
        }

        for (int i = elitismCount; i < _populationSize; i++)
        {
            var parent1 = TournamentSelect();
            var parent2 = TournamentSelect();
            var child = nextPop[i];

            for (int j = 0; j < _schema.Count; j++)
            {
                child.Genes[j] = _mainRng.NextDouble() < _crossoverRate ? parent1.Genes[j] : parent2.Genes[j];
            }

            for (int j = 0; j < _schema.Count; j++)
            {
                if (_mainRng.NextDouble() < currentMutRate)
                {
                    var attr = _schema[j];
                    child.Genes[j] = GeneInjector.GenerateRandomGene(_mainRng, attr.Min, attr.Max, attr.Step);
                }
            }

            child.Generation = _currentGeneration + 1;
            child.IndividualIndex = i;
            child.Fitness = Chromosome.NotEvaluated;
            child.Seed = 0;
        }

        _population = nextPop;
        _currentGeneration++;
        _evaluated = false;
    }

    /// <summary>Saves current state.</summary>
    public GeneticOptimizerState SaveState() => new()
    {
        Population = [.. _population.Select(c => c.Clone())],
        CurrentGeneration = _currentGeneration,
        Evaluated = _evaluated,
        BestOverallFitness = _bestOverallFitness,
        StagnationCount = _stagnationCount,
        HyperMutation = _hyperMutation
    };

    /// <summary>Restores state.</summary>
    public void LoadState(GeneticOptimizerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _population = [.. state.Population.Select(c => c.Clone())];
        _currentGeneration = state.CurrentGeneration;
        _evaluated = state.Evaluated;
        _bestOverallFitness = state.BestOverallFitness;
        _stagnationCount = state.StagnationCount;
        _hyperMutation = state.HyperMutation;
    }

    /// <summary>Marks fitness as dirty.</summary>
    public void InvalidateFitness()
    {
        foreach (var c in _population)
        {
            c.Fitness = Chromosome.NotEvaluated;
        }

        _evaluated = false;
    }

    private void Sort() => Array.Sort(_population, (a, b) => b.Fitness.CompareTo(a.Fitness));
    private int GenerateIndividualSeed(int index) => (int)(((uint)_masterSeed * 397) ^ (uint)index);

    private Chromosome TournamentSelect()
    {
        Chromosome best = _population[_mainRng.Next(_populationSize)];
        for (int i = 1; i < _tournamentSize; i++)
        {
            var competitor = _population[_mainRng.Next(_populationSize)];
            if (competitor.Fitness > best.Fitness)
            {
                best = competitor;
            }
        }
        return best;
    }
}
