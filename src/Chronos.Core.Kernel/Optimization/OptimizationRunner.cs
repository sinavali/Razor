using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Chronos.Core.Kernel.Configuration;
using Chronos.Core.Kernel.Events;
using Chronos.Core.Kernel.Hooks;
using Chronos.Core.Kernel.Messaging;
using Chronos.Core.Kernel.Reporting;
using Chronos.Core.Kernel.Telemetry;

namespace Chronos.Core.Kernel.Optimization;

/// <summary>
/// Orchestrates a full genetic optimization run.
/// Handles generation loop, fitness evaluation via hooks, and state persistence.
/// </summary>
public sealed class OptimizationRunner
{
    private readonly OptimizationSpecification _spec;
    private readonly GeneticOptimizer _ga;
    private readonly IOptimizationHooks? _hooks;
    private readonly ICoreMetrics _metrics;
    private readonly INeuralNetworkModel? _neuralNetwork;
    private readonly IMessageBus? _messageBus;
    private readonly IHookRegistry? _hookRegistry;

    /// <summary>
    /// Creates a new runner.
    /// </summary>
    public OptimizationRunner(
        OptimizationSpecification spec,
        IReadOnlyList<GeneAttribute> schema,
        CoreMetrics metrics,
        IOptimizationHooks? hooks = null,
        INeuralNetworkModel? neuralNetwork = null,
        IMessageBus? messageBus = null,
        IHookRegistry? hookRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(spec);

        _spec = spec;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _hooks = hooks;
        _neuralNetwork = neuralNetwork;
        _messageBus = messageBus;
        _hookRegistry = hookRegistry;

        _ga = new GeneticOptimizer(
            schema,
            metrics,
            spec.PopulationSize,
            spec.MasterSeed,
            spec.MutationRate,
            spec.CrossoverRate,
            spec.ElitismPct,
            spec.TournamentSize,
            spec.StagnationGenerationsBeforeHyper,
            spec.MaxParallelThreads,
            hooks);
    }

    /// <summary>
    /// Runs the optimization loop.
    /// </summary>
    public async Task<Chromosome> RunAsync(
        Func<Chromosome, CancellationToken, Task<double>> fitnessEvaluator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fitnessEvaluator);

        var systemClock = new Clock.SystemClock(); // single clock for all hooks
        _ga.Initialize();

        // optimisation.started hook
        _hooks?.OnStart.InvokeActionChain(
            new OptimizationContext(systemClock, 0, _spec.Generations, _spec.PopulationSize,
                Chromosome.NotEvaluated, false, "optimization.started"));

        for (int gen = 0; gen < _spec.Generations; gen++)
        {
            ct.ThrowIfCancellationRequested();

            _hooks?.OnGenerationStart.InvokeActionChain(
                gen,
                new OptimizationContext(systemClock, gen, _spec.Generations, _spec.PopulationSize,
                    _ga.BestSolution?.Fitness ?? Chromosome.NotEvaluated, _ga.IsHyperMutation,
                    "optimization.generation.start"));

            // Evaluate population
            await _ga.EvaluateAsync(async (chromo, innerCt) =>
            {
                double defaultFitness = await fitnessEvaluator(chromo, innerCt).ConfigureAwait(false);

                // Build fitness evaluation context and fire hook
                var fitnessCtx = new FitnessEvaluationContext(
                    systemClock,
                    new Chronos.Core.Abstractions.Hooks.Chromosome
                    {
                        Genes = chromo.Genes,
                        Fitness = double.NaN,
                        Generation = chromo.Generation,
                        IndividualIndex = chromo.IndividualIndex,
                        Seed = chromo.Seed
                    },
                    "optimization.fitness.evaluate");

                // Use the interface directly now that it's available
                _hooks?.OnFitnessEvaluation.InvokeActionChain(
                    (IFitnessEvaluationContext)fitnessCtx,
                    new OptimizationContext(systemClock, gen, _spec.Generations, _spec.PopulationSize,
                        _ga.BestSolution?.Fitness ?? defaultFitness, _ga.IsHyperMutation,
                        "optimization.fitness.evaluate"));

                double finalFitness = double.IsNaN(fitnessCtx.Fitness) ? defaultFitness : fitnessCtx.Fitness;

                // Notify evaluators (post‑fitness)
                _hooks?.OnChromosomeEvaluated.InvokeActionChain(
                    (new Chronos.Core.Abstractions.Hooks.Chromosome
                    {
                        Genes = chromo.Genes,
                        Fitness = finalFitness,
                        Generation = chromo.Generation,
                        IndividualIndex = chromo.IndividualIndex,
                        Seed = chromo.Seed
                    }, finalFitness),
                    new OptimizationContext(systemClock, gen, _spec.Generations, _spec.PopulationSize,
                        _ga.BestSolution?.Fitness ?? finalFitness, _ga.IsHyperMutation,
                        "optimization.chromosome.evaluated"));

                return finalFitness;
            }, ct).ConfigureAwait(false);

            if (_ga.IsHyperMutation)
            {
                _hooks?.OnStagnationDetected.InvokeActionChain(
                    gen,
                    new OptimizationContext(systemClock, gen, _spec.Generations, _spec.PopulationSize,
                        _ga.BestSolution!.Fitness, true, "optimization.stagnation"));
            }

            _hooks?.OnGenerationCompleted.InvokeActionChain(
                (gen, _ga.BestSolution!.Fitness, _ga.IsHyperMutation),
                new OptimizationContext(systemClock, gen, _spec.Generations, _spec.PopulationSize,
                    _ga.BestSolution!.Fitness, _ga.IsHyperMutation, "optimization.generation.completed"));

            // Publish generation event
            _messageBus?.Publish(new OptimizationGenerationEvent
            {
                Timestamp = systemClock.GetUtcNow(),
                Generation = gen,
                BestFitness = _ga.BestSolution!.Fitness,
                IsHyperMutation = _ga.IsHyperMutation,
                EventId = $"opt-gen-{gen}-{Guid.NewGuid():N}"
            });

            if (gen < _spec.Generations - 1)
            {
                _ga.Evolve();
            }
        }

        var best = _ga.BestSolution!;

        _hooks?.OnCompleted.InvokeActionChain(
            new Chronos.Core.Abstractions.Hooks.Chromosome
            {
                Genes = best.Genes,
                Fitness = best.Fitness,
                Generation = best.Generation,
                IndividualIndex = best.IndividualIndex,
                Seed = best.Seed
            },
            new OptimizationContext(systemClock, _spec.Generations - 1, _spec.Generations, _spec.PopulationSize,
                best.Fitness, _ga.IsHyperMutation, "optimization.completed"));

        // Publish cycle completed event
        _messageBus?.Publish(new OptimizationCycleCompletedEvent(0, best.Fitness, _spec.Generations)
        {
            Timestamp = systemClock.GetUtcNow(),
            CorrelationId = Guid.NewGuid(),
            EventId = $"opt-cycle-{Guid.NewGuid():N}"
        });

        // Generate report if hooks are available
        if (_hookRegistry is not null)
        {
            var reportGen = new ReportGenerator(_hookRegistry.Report, systemClock);
            reportGen.GenerateOptimizationReport(best, _spec);
        }

        return best;
    }

    /// <summary>
    /// Saves the current GA state, including serialized neural network state if present.
    /// </summary>
    public GeneticOptimizerState SaveState()
    {
        var state = _ga.SaveState();
        if (_neuralNetwork is not null)
        {
            try
            {
                state = state with { NeuralNetworkState = _neuralNetwork.SerializeState() };
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                // Best effort
            }
        }

        return state;
    }

    /// <summary>
    /// Restores the GA state, including neural network state if present.
    /// </summary>
    public void LoadState(GeneticOptimizerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _ga.LoadState(state);

        if (state.NeuralNetworkState is not null && _neuralNetwork is not null)
        {
            try
            {
                _neuralNetwork.DeserializeState(state.NeuralNetworkState);
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                // Best effort
            }
        }
    }
}
