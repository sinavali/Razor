using Chronos.Core.Adapters;
using Chronos.Core.Configuration;
using Chronos.Core.Data;
using Chronos.Core.Neural;
using Chronos.Core.Optimization;
using Chronos.Core.Strategies;
using Chronos.Core.Trading;
using Chronos.Messaging;
using Chronos.Messaging.Events;
using Chronos.Orchestration.HealthChecks;
using Chronos.Orchestration.Reporting;
using Chronos.Orchestration.Utils;
using Chronos.Sdk.Backtesting;
using Chronos.Sdk.Brokers;
using Chronos.Sdk.Clock;
using Chronos.Sdk.Metrics;
using Chronos.Sdk.Optimization;
using Chronos.Sdk.Telemetry;
using System.Diagnostics;
using Chronos.Orchestration.Hosting;
using Microsoft.Extensions.Logging;

namespace Chronos.Orchestration.Pipelines;

/// <summary>
/// Live trading pipeline: connect to broker, subscribe to tick data,
/// optionally run continuous optimisation in the background.
/// </summary>
public static class LivePipeline
{
    // LoggerMessage delegates for performance (CA1848)
    private static readonly Action<ILogger, string, Exception?> LogUnobservedBrokerError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "UnobservedBroker"),
            "Unobserved error in broker.OnTickAsync for {Symbol}");
    private static readonly Action<ILogger, string, Exception?> LogUnobservedStrategyError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "UnobservedStrategy"),
            "Unobserved error in strategy.OnTickAsync for {Symbol}");
    private static readonly Action<ILogger, string, Exception?> LogTickProcessingError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "TickProcessing"),
            "Error processing tick for {Symbol}");
    private static readonly Action<ILogger, string, Exception?> LogReconnectWarning =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "ReconnectWarning"),
            "{Message}");
    private static readonly Action<ILogger, string, Exception?> LogReconnectError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(5, "ReconnectError"),
            "{Message}");
    private static readonly Action<ILogger, string, Exception?> LogReconnectInfo =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "ReconnectInfo"),
            "{Message}");
    private static readonly Action<ILogger, string, Exception?> LogCriticalLoss =
        LoggerMessage.Define<string>(LogLevel.Critical, new EventId(7, "CriticalLoss"),
            "{Message}");
    private static readonly Action<ILogger, string, Exception?> LogContinuousOptError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(8, "ContinuousOptError"),
            "{Message}");
    private static readonly Action<ILogger, string, Exception?> LogOptError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, "OptError"),
            "{Message}");

    /// <summary>Runs a live trading session until cancellation or permanent connection loss.</summary>
    public static async Task ExecuteAsync(
        IAdapter adapter, IStrategy strategy, StrategySpecification strategySpec,
        LiveSpecification liveSpec, ExecutionSpecification execSpec, OptimizationSpecification optSpec,
        IBacktestRunner runner, IMetricsCalculator metricsCalc, IReportExporter exporter,
        int[]? neuralTopology = null, ActivationFunction neuralActivation = ActivationFunction.Tanh,
        IMessageBus? messageBus = null, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        TaskExceptionHandler.Register(logger);

        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(strategySpec);
        ArgumentNullException.ThrowIfNull(liveSpec);
        ArgumentNullException.ThrowIfNull(execSpec);
        ArgumentNullException.ThrowIfNull(optSpec);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(metricsCalc);
        ArgumentNullException.ThrowIfNull(exporter);

        var marketClock = new TickClock();
        var wallClock = new SystemClock();

        var broker = new LiveBroker(adapter, liveSpec.MagicNumber, strategySpec.Leverage,
            marketClock, wallClock,
            liveSpec.NotificationChannels, messageBus, liveSpec.OrderGuardTimeoutSeconds);

        using var optimisationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            if (strategy is StrategyBase sb)
            {
                sb.WireUp(broker, null!);
                await sb.OnConfigureAsync(strategySpec).ConfigureAwait(false);
                var indicatorRegistry = Chronos.Core.Indicators.IndicatorRegistryFactory.Create();
                await sb.OnStartAsync(indicatorRegistry).ConfigureAwait(false);
            }

            if (!await adapter.ConnectAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Could not connect to adapter.");
            ChronosMetrics.SetConnectionState(true, adapter.AdapterName);
            messageBus?.Publish(new ConnectionStateEvent(true, adapter.AdapterName));

            var symbols = strategySpec.RequestedSymbols.Select(r => r.Symbol).ToArray();
            var timeframes = strategySpec.RequestedSymbols.SelectMany(r => r.TimeFrames).Distinct()
                .Where(tf => tf != TimeFrame.Tick).ToList();
            using var tickWindow = new TickWindow(symbols, timeframes);
            if (strategy is StrategyBase sbLive) sbLive.WireUp(broker, tickWindow);

            tickWindow.WindowCompleted += (symbol, tf) =>
            {
                if (strategy is StrategyBase s) _ = s.NotifyWindowCompletedAsync(symbol, tf);
            };

            await broker.InitializeLiveStateAsync(cancellationToken).ConfigureAwait(false);
            LiveMonitoringState.Instance.ConfiguredIntervalHours = liveSpec.OptimizationIntervalHours;

            foreach (var req in strategySpec.RequestedSymbols)
            {
                var props = await adapter.GetSymbolPropertiesAsync(req.Symbol, cancellationToken).ConfigureAwait(false);
                var spec = props ?? new SymbolProperties
                { ContractSize = 1, TickSize = 0.0001, TickValue = 0.001, MinVolume = 0.0001, MaxLeverage = 100 };

                var supportedTimeframes = adapter.GetSupportedTimeframes(req.Symbol);
                if (supportedTimeframes is { Length: > 0 })
                {
                    var unsupported = req.TimeFrames.Where(tf => !supportedTimeframes.Contains(tf)).ToArray();
                    if (unsupported.Length > 0)
                    {
                        // Log a warning for unsupported timeframes; the adapter may not provide data for them.
                        System.Diagnostics.Trace.TraceWarning(
                            $"Symbol '{req.Symbol}' requested timeframes [{string.Join(", ", unsupported)}] which are not in the adapter's supported list.");
                    }
                }

                await broker.SetSymbolSpecsAsync(req.Symbol, spec).ConfigureAwait(false);
            }

            foreach (var req in strategySpec.RequestedSymbols)
                await adapter.SubscribeAsync(req.Symbol).ConfigureAwait(false);

            void onTick(string symbol, Tick tick)
            {
                var latency = DateTime.UtcNow.Ticks - tick.Time;
                ChronosMetrics.RecordLiveTickLatency(latency);

                LiveMonitoringState.Instance.MarkTickReceived();
                ReadLockSlimHelper.EnterReadLock((strategy as StrategyBase)?.GeneLock);
                try
                {
                    broker.OnTickAsync(symbol, tick).ContinueWith(t =>
                    {
                        if (t.IsFaulted) LogUnobservedBrokerError(logger!, symbol, t.Exception);
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

                    tickWindow.PushTick(symbol, tick);

                    strategy.OnTickAsync(tick).ContinueWith(t =>
                    {
                        if (t.IsFaulted) LogUnobservedStrategyError(logger!, symbol, t.Exception);
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                }
#pragma warning disable CA1031 // Catch general exceptions to prevent live engine crash
                catch (Exception ex)
                {
                    LogTickProcessingError(logger!, symbol, ex);
                }
#pragma warning restore CA1031
                finally
                {
                    ReadLockSlimHelper.ExitReadLock((strategy as StrategyBase)?.GeneLock);
                }
            }

            adapter.OnTickReceived += onTick;

            Task? continuousTask = null;
            if (liveSpec.ContinuousOptimization)
            {
                continuousTask = Task.Run(() =>
                    ContinuousOptimizationLoop(adapter, strategy, strategySpec, liveSpec, execSpec, optSpec,
                        runner, metricsCalc, exporter, neuralTopology, neuralActivation,
                        logger, messageBus, optimisationCts.Token), optimisationCts.Token);
            }

            int reconnectRetries = 0;
            const int maxReconnectRetries = 10;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!adapter.IsConnected)
                {
                    LogReconnectWarning(logger!, "Adapter connection lost. Attempting reconnection...", null);
                    adapter.OnTickReceived -= onTick;
                    bool reconnected = false;
                    while (!cancellationToken.IsCancellationRequested && reconnectRetries < maxReconnectRetries)
                    {
                        try
                        {
                            if (await adapter.ConnectAsync(cancellationToken).ConfigureAwait(false))
                            {
                                ChronosMetrics.SetConnectionState(true, adapter.AdapterName);
                                messageBus?.Publish(new LiveReconnectEvent(true, reconnectRetries,
                                    adapter.AdapterName));
                                await broker.ReconcileAsync(cancellationToken).ConfigureAwait(false);
                                foreach (var req in strategySpec.RequestedSymbols)
                                    await adapter.SubscribeAsync(req.Symbol).ConfigureAwait(false);
                                adapter.OnTickReceived += onTick;
                                reconnected = true;
                                reconnectRetries = 0;
                                LogReconnectInfo(logger!, "Reconnection successful.", null);
                                break;
                            }
                        }
#pragma warning disable CA1031 // Catch general exceptions to log and continue reconnection attempts
                        catch (Exception ex)
                        {
                            LogReconnectError(logger!, $"Reconnect attempt {reconnectRetries + 1} failed.", ex);
                        }
#pragma warning restore CA1031

                        reconnectRetries++;
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, reconnectRetries) * 1000), cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (!reconnected)
                    {
                        messageBus?.Publish(new LiveReconnectEvent(false, reconnectRetries, adapter.AdapterName));
                        LogCriticalLoss(logger!, "Permanent connection loss after max attempts.", null);
#pragma warning disable CA1849 // Reason: Cancel must be called synchronously from the connection monitor; no async alternative needed.
                        optimisationCts.Cancel();
#pragma warning restore CA1849
                        break;
                    }
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }

            if (continuousTask is not null)
            {
                try
                {
                    await continuousTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
#pragma warning disable CA1031 // Catch general exceptions to prevent live engine crash on background task
                catch (Exception ex)
                {
                    LogContinuousOptError(logger!, "Error shutting down background optimisation.", ex);
                }
#pragma warning restore CA1031
            }

            adapter.OnTickReceived -= onTick;
            foreach (var req in strategySpec.RequestedSymbols)
                await broker.CloseAllAsync(req.Symbol).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await broker.DisposeAsync().ConfigureAwait(false);
            ChronosMetrics.SetConnectionState(false, adapter.AdapterName);
            messageBus?.Publish(new ConnectionStateEvent(false, adapter.AdapterName));
            await strategy.OnStopAsync().ConfigureAwait(false);
        }
    }

    private static async Task<(double[]? Genes, double BestFitness)> RunOptimizationCycleAsync(
    IAdapter adapter, IStrategy strategy, StrategySpecification strategySpec,
    LiveSpecification liveSpec, ExecutionSpecification execSpec, OptimizationSpecification optSpec,
    IBacktestRunner runner, int[]? neuralTopology, ActivationFunction neuralActivation,
    IMessageBus? messageBus, int cycleIndex, CancellationToken cancellationToken)
    {
        DateTime end = DateTime.UtcNow.AddDays(-liveSpec.SkipRecentDays);
        DateTime start = end.AddDays(-liveSpec.LookbackDays);
        var symbols = strategySpec.RequestedSymbols.Select(r => r.Symbol).Distinct().ToArray();
        var borrowed = await DataFetchHelper.FetchAllAsync(
            adapter, symbols, start, end, DataActionPolicy.DeleteAfterTask, cancellationToken).ConfigureAwait(false);
        try
        {
            if (borrowed.Streams.Length == 0) return (null, double.NaN);

            int seed = liveSpec.RotateOptimizationSeed
                ? optSpec.MasterSeed + cycleIndex
                : optSpec.MasterSeed;

            var symbolProperties = await FetchSymbolPropertiesAsync(adapter, strategySpec.RequestedSymbols, cancellationToken)
                .ConfigureAwait(false);

            var schema = GeneInjector.BuildCompleteSchema(strategy.GetType(), neuralTopology, neuralActivation);
            var optimizer = new GeneticOptimizer(schema, optSpec.PopulationSize, seed,
                optSpec.MutationRate, optSpec.CrossoverRate, optSpec.ElitismPct, optSpec.TournamentSize,
                stagnationGenerationsBeforeHyper: optSpec.StagnationGenerationsBeforeHyper);
            optimizer.Initialize();

            async Task<double> Evaluator(Chromosome c, CancellationToken ct)
            {
                var input = new BacktestInput
                {
                    TickStreams = borrowed.Streams,
                    Symbols = borrowed.Symbols,
                    Strategy = (IStrategy)Activator.CreateInstance(strategy.GetType())!,
                    StrategySpecification = strategySpec,
                    ExecutionSpecification = execSpec,
                    MarketCalculator = adapter.Calculator,
                    SymbolProperties = symbolProperties,
                    Genes = c.Genes,
                    GeneInitializationSeed = execSpec.GeneInitializationSeed,
                    MessageBus = messageBus
                };
                var result = await runner.RunAsync(input, ct).ConfigureAwait(false);
                return FitnessCalculator.Calculate(result, optSpec.FitnessModel, strategySpec.InitialBalance);
            }

            double? prevBest = null;
            for (int gen = 0; gen < optSpec.Generations; gen++)
            {
                await optimizer.EvaluateAsync(Evaluator, cancellationToken).ConfigureAwait(false);
                optimizer.Evolve();
                double best = optimizer.BestSolution.Fitness;
                if (prevBest.HasValue) ChronosMetrics.RecordGaFitnessImprovement(best - prevBest.Value);
                prevBest = best;
                messageBus?.Publish(new OptimizationGenerationEvent
                { Generation = gen, BestFitness = best, IsHyperMutation = optimizer.IsHyperMutation });
            }

            return (optimizer.BestSolution.Genes, optimizer.BestSolution.Fitness);
        }
        finally
        {
            await borrowed.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task ContinuousOptimizationLoop(
        IAdapter adapter, IStrategy strategy, StrategySpecification strategySpec,
        LiveSpecification liveSpec, ExecutionSpecification execSpec, OptimizationSpecification optSpec,
        IBacktestRunner runner, IMetricsCalculator _, IReportExporter __,
        int[]? neuralTopology, ActivationFunction neuralActivation,
        ILogger? logger, IMessageBus? messageBus, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(liveSpec.InitialDelayMinutes), cancellationToken).ConfigureAwait(false);
        int cycleIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                var (bestGenes, bestFitness) = await RunOptimizationCycleAsync(
                        adapter, strategy, strategySpec, liveSpec, execSpec, optSpec,
                        runner, neuralTopology, neuralActivation, messageBus, cycleIndex, cancellationToken)
                    .ConfigureAwait(false);

                if (bestGenes is not null)
                {
                    (strategy as StrategyBase)?.GeneLock.EnterWriteLock();
                    try
                    {
                        strategy.InjectGenes(bestGenes);
                    }
                    finally
                    {
                        (strategy as StrategyBase)?.GeneLock.ExitWriteLock();
                    }

                    LiveMonitoringState.Instance.MarkOptimisationCompleted();
                    LiveMonitoringState.Instance.MarkOptimisationStatus(true);
                }
                else
                {
                    LiveMonitoringState.Instance.MarkOptimisationStatus(false);
                }

                messageBus?.Publish(new OptimizationCycleCompletedEvent(
                    cycleIndex, double.IsNaN(bestFitness) ? 0.0 : bestFitness, optSpec.Generations));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // Catch general exceptions to log and continue optimisation loop
            catch (Exception ex)
            {
                LogOptError(logger!, "Live optimisation error.", ex);
                LiveMonitoringState.Instance.MarkOptimisationStatus(false);
                messageBus?.Publish(new OptimizationCycleCompletedEvent(cycleIndex, double.NaN, optSpec.Generations));
            }
#pragma warning restore CA1031
            finally
            {
                sw.Stop();
                ChronosMetrics.RecordOptimizationDuration(sw.Elapsed.TotalSeconds);
            }

            cycleIndex++;
            await Task.Delay(TimeSpan.FromHours(liveSpec.OptimizationIntervalHours), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<Dictionary<string, SymbolProperties>> FetchSymbolPropertiesAsync(
        IAdapter adapter, IReadOnlyList<SymbolRequest> requests, CancellationToken cancellationToken)
    {
        var specs = new Dictionary<string, SymbolProperties>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in requests)
        {
            var props = await adapter.GetSymbolPropertiesAsync(req.Symbol, cancellationToken).ConfigureAwait(false);
            specs[req.Symbol] = props ?? new SymbolProperties
            { ContractSize = 1, TickSize = 0.0001, TickValue = 0.001, MinVolume = 0.0001, MaxLeverage = 100 };

            var supportedTimeframes = adapter.GetSupportedTimeframes(req.Symbol);
            if (supportedTimeframes is { Length: > 0 })
            {
                var unsupported = req.TimeFrames.Where(tf => !supportedTimeframes.Contains(tf)).ToArray();
                if (unsupported.Length > 0)
                {
                    // Log a warning for unsupported timeframes; the adapter may not provide data for them.
                    System.Diagnostics.Trace.TraceWarning(
                        $"Symbol '{req.Symbol}' requested timeframes [{string.Join(", ", unsupported)}] which are not in the adapter's supported list.");
                }
            }
        }
        return specs;
    }
}

/// <summary>
/// Utility for safely acquiring/releasing a read lock when it might be null.
/// </summary>
internal static class ReadLockSlimHelper
{
    public static void EnterReadLock(ReaderWriterLockSlim? lck)
    {
        if (lck != null) lck.EnterReadLock();
    }

    public static void ExitReadLock(ReaderWriterLockSlim? lck)
    {
        if (lck != null) lck.ExitReadLock();
    }
}
