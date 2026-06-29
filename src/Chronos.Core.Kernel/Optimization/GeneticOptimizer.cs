using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Kernel.Clock;
using Chronos.Core.Kernel.Hooks;
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
    private readonly CustomizedRandom _mainRng;
    private readonly CoreMetrics _metrics;
    private readonly IOptimizationHooks? _hooks;

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
    private const double RelativeFitnessTolerance = 1e-6;
    private readonly SystemClock _systemClock = new();

    /// <inheritdoc/>
    public int CurrentGeneration => _currentGeneration;

    /// <inheritdoc/>
    public int PopulationSize => _populationSize;

    /// <inheritdoc/>
    public bool IsHyperMutation => _hyperMutation;

    /// <inheritdoc/>
    public Chromosome BestSolution
    {
        get
        {
            Sort();
            return _population[0];
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Chromosome> Population
    {
        get
        {
            Sort();
            return _population;
        }
    }

    /// <summary>Configures threading limits for parallel evaluations.</summary>
    public int MaxDegreeOfParallelism { get; set; }

    /// <summary>Initializes a new optimizer instance.</summary>
    public GeneticOptimizer(
        IReadOnlyList<GeneAttribute> schema,
        CoreMetrics metrics,
        int populationSize = 100,
        int masterSeed = 0,
        double mutationRate = 0.1,
        double crossoverRate = 0.5,
        double elitismPct = 0.05,
        int tournamentSize = 3,
        int stagnationGenerationsBeforeHyper = 3,
        int maxDegreeOfParallelism = 0,
        IOptimizationHooks? hooks = null)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _hooks = hooks;
        _populationSize = Math.Max(4, populationSize);
        _masterSeed = masterSeed;
        _mutationRate = mutationRate;
        _crossoverRate = crossoverRate;
        _elitismPct = elitismPct;
        _tournamentSize = Math.Max(2, tournamentSize);
        _stagnationGenerationsBeforeHyper = stagnationGenerationsBeforeHyper;

        _mainRng = new CustomizedRandom(masterSeed);
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

        var hookContext = new OptimizationContext(_systemClock, 0, 0, _populationSize, Chromosome.NotEvaluated, false, "optimization.chromosome.created");

        for (int i = 0; i < _populationSize; i++)
        {
            var c = _population[i];
            c.Generation = 0;
            c.IndividualIndex = i;
            c.Fitness = Chromosome.NotEvaluated;

            // Generate a valid chromosome by retrying if filter rejects
            bool accepted = false;
            int attempt = 0;
            while (!accepted)
            {
                int individualSeed = GenerateIndividualSeed(i + attempt * _populationSize);
                c.Seed = individualSeed;
                var indRng = new CustomizedRandom(individualSeed);

                for (int j = 0; j < _schema.Count; j++)
                {
                    var attr = _schema[j];
                    c.Genes[j] = GeneInjector.GenerateRandomGene(indRng, attr.Min, attr.Max, attr.Step);
                }

                // Invoke filter
                if (_hooks is not null)
                {
                    var wrapped = new Chronos.Core.Abstractions.Hooks.Chromosome
                    {
                        Genes = c.Genes,
                        Fitness = c.Fitness,
                        Generation = c.Generation,
                        IndividualIndex = c.IndividualIndex,
                        Seed = c.Seed
                    };
                    var result = _hooks.OnChromosomeCreated.InvokeFilterChain(wrapped, hookContext);
                    if (result.IsAllowed)
                    {
                        // Apply modified genes if any
                        if (result.Data is not null)
                        {
                            Array.Copy(result.Data.Genes, c.Genes, c.Genes.Length);
                            c.Fitness = result.Data.Fitness;
                            c.Generation = result.Data.Generation;
                            c.IndividualIndex = result.Data.IndividualIndex;
                            c.Seed = result.Data.Seed;
                        }
                        accepted = true;
                    }
                    else
                    {
                        // Regenerate with a new seed and try again
                        attempt++;
                    }
                }
                else
                {
                    accepted = true; // no filter, accept as is
                }
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
            async (c, innerCt) =>
            {
                c.Fitness = await evaluator(c, innerCt).ConfigureAwait(false);
            }).ConfigureAwait(false);

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

        // Elitism
        for (int i = 0; i < elitismCount; i++)
        {
            nextPop[i].CopyFrom(_population[i]);
            nextPop[i].Generation = _currentGeneration + 1;
            nextPop[i].IndividualIndex = i;
        }

        var hookContext = new OptimizationContext(_systemClock, _currentGeneration, 0, _populationSize, _bestOverallFitness, _hyperMutation, "optimization");

        // Selection, crossover, mutation
        for (int i = elitismCount; i < _populationSize; i++)
        {
            // Selection
            var parent1 = TournamentSelect();
            var parent2 = TournamentSelect();
            _hooks?.OnSelectionApplied.InvokeActionChain(
                (new Chronos.Core.Abstractions.Hooks.Chromosome
                {
                    Genes = parent1.Genes,
                    Fitness = parent1.Fitness,
                    Generation = parent1.Generation,
                    IndividualIndex = parent1.IndividualIndex,
                    Seed = parent1.Seed
                },
                new Chronos.Core.Abstractions.Hooks.Chromosome
                {
                    Genes = parent2.Genes,
                    Fitness = parent2.Fitness,
                    Generation = parent2.Generation,
                    IndividualIndex = parent2.IndividualIndex,
                    Seed = parent2.Seed
                }),
                new OptimizationContext(_systemClock, _currentGeneration, 0, _populationSize, _bestOverallFitness, _hyperMutation, "optimization.selection"));

            var child = nextPop[i];

            // Crossover
            for (int j = 0; j < _schema.Count; j++)
            {
                child.Genes[j] = _mainRng.NextDouble() < _crossoverRate ? parent1.Genes[j] : parent2.Genes[j];
            }

            _hooks?.OnCrossoverApplied.InvokeActionChain(
                new Chronos.Core.Abstractions.Hooks.Chromosome
                {
                    Genes = child.Genes,
                    Fitness = child.Fitness,
                    Generation = child.Generation,
                    IndividualIndex = child.IndividualIndex,
                    Seed = child.Seed
                },
                new OptimizationContext(_systemClock, _currentGeneration, 0, _populationSize, _bestOverallFitness, _hyperMutation, "optimization.crossover"));

            // Mutation
            for (int j = 0; j < _schema.Count; j++)
            {
                if (_mainRng.NextDouble() < currentMutRate)
                {
                    var attr = _schema[j];
                    child.Genes[j] = GeneInjector.GenerateRandomGene(_mainRng, attr.Min, attr.Max, attr.Step);
                }
            }

            _hooks?.OnMutationApplied.InvokeActionChain(
                new Chronos.Core.Abstractions.Hooks.Chromosome
                {
                    Genes = child.Genes,
                    Fitness = child.Fitness,
                    Generation = child.Generation,
                    IndividualIndex = child.IndividualIndex,
                    Seed = child.Seed
                },
                new OptimizationContext(_systemClock, _currentGeneration, 0, _populationSize, _bestOverallFitness, _hyperMutation, "optimization.mutation"));

            child.Generation = _currentGeneration + 1;
            child.IndividualIndex = i;
            child.Fitness = Chromosome.NotEvaluated;
            child.Seed = 0;
        }

        _population = nextPop;
        _currentGeneration++;
        _evaluated = false;
    }

    /// <summary>Saves current state to a snapshot for pause/resume.</summary>
    public GeneticOptimizerState SaveState()
    {
        return new GeneticOptimizerState
        {
            Population = [.. _population.Select(c => c.Clone())],
            CurrentGeneration = _currentGeneration,
            Evaluated = _evaluated,
            BestOverallFitness = _bestOverallFitness,
            StagnationCount = _stagnationCount,
            HyperMutation = _hyperMutation
        };
    }

    /// <summary>Restores state from a previously saved snapshot.</summary>
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

    /// <summary>Marks all chromosomes as unevaluated.</summary>
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
