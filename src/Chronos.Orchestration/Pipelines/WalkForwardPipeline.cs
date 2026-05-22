using Chronos.Core.Adapters;
using Chronos.Core.Configuration;
using Chronos.Core.Neural;
using Chronos.Core.Optimization;
using Chronos.Core.Strategies;
using Chronos.Core.Trading;
using Chronos.Core.Utils;
using Chronos.Messaging;
using Chronos.Messaging.Events;
using Chronos.Orchestration.HealthChecks;
using Chronos.Orchestration.Reporting;
using Chronos.Orchestration.Utils;
using Chronos.Sdk.Backtesting;
using Chronos.Sdk.Metrics;
using Chronos.Sdk.Optimization;
using Chronos.Sdk.Telemetry;
using System.Diagnostics;

namespace Chronos.Orchestration.Pipelines;

/// <summary>
/// Walk‑forward analysis pipeline: rolling train/test windows, GA optimisation,
/// and stitched performance reporting. Supports pause/resume via <see cref="GeneticOptimizerState"/>.
/// </summary>
public static class WalkForwardPipeline
{
    /// <summary>
    /// Runs a complete walk‑forward analysis and returns the report stream together with the final optimizer state.
    /// </summary>
    public static async Task<(Stream Report, GeneticOptimizerState? FinalState)> ExecuteAsync(
        IAdapter adapter,
        Type strategyType,
        StrategySpecification strategySpec,
        ExecutionSpecification execSpec,
        OptimizationSpecification optSpec,
        IBacktestRunner runner,
        IMetricsCalculator metricsCalc,
        IReportExporter exporter,
        int[]? neuralTopology = null,
        ActivationFunction neuralActivation = ActivationFunction.Tanh,
        IMessageBus? messageBus = null,
        GeneticOptimizerState? optimizerState = null,
        IProgress<BacktestProgress>? backtestProgress = null,
        IProgress<OptimizationProgress>? optimizationProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(strategyType);
        ArgumentNullException.ThrowIfNull(strategySpec);
        ArgumentNullException.ThrowIfNull(execSpec);
        ArgumentNullException.ThrowIfNull(optSpec);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(metricsCalc);
        ArgumentNullException.ThrowIfNull(exporter);

        // 1. Fetch symbol properties
        await adapter.ConnectAsync(cancellationToken).ConfigureAwait(false);
        var symbolProps = new Dictionary<string, SymbolProperties>();
        foreach (var req in strategySpec.RequestedSymbols)
        {
            var props = await adapter.GetSymbolPropertiesAsync(req.Symbol, cancellationToken).ConfigureAwait(false);
            symbolProps[req.Symbol] = props ?? new SymbolProperties
            { ContractSize = 1, TickSize = 0.0001, TickValue = 0.001, MinVolume = 0.0001, MaxLeverage = 100 };

            var supportedTimeframes = adapter.GetSupportedTimeframes(req.Symbol);
            if (supportedTimeframes is { Length: > 0 })
            {
                var unsupported = req.TimeFrames.Where(tf => !supportedTimeframes.Contains(tf)).ToArray();
                if (unsupported.Length > 0)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"Symbol '{req.Symbol}' requested timeframes [{string.Join(", ", unsupported)}] which are not in the adapter's supported list.");
                }
            }
        }

        var symbols = strategySpec.RequestedSymbols.Select(r => r.Symbol).Distinct().ToList();

        // 2. Build gene schema
        var schema = GeneInjector.BuildCompleteSchema(strategyType, neuralTopology, neuralActivation);
        var popHistory = new List<GenerationSnapshot>();
        var windowSummaries = new List<object>();

        // 3. Calculate window sizes based on total available ticks
        long totalTicks = (execSpec.EndDate - execSpec.StartDate).Ticks;
        long windowSize = totalTicks / optSpec.Windows;
        long trainSize = (long)(windowSize * optSpec.TrainSplit);
        long testSize = windowSize - trainSize;

        if (windowSize <= 0 || trainSize <= 0 || testSize <= 0)
        {
            throw new ConfigurationException("Date range too short for Walk‑Forward windows.");
        }

        double stitchedBalance = strategySpec.InitialBalance;
        double peakBalance = stitchedBalance;
        double maxStitchedDD = 0;
        var allOosTrades = new List<Position>();
        GeneticOptimizerState? finalState = null;
        GeneticOptimizerState? previousState = optimizerState;

        Stopwatch totalSw = Stopwatch.StartNew();

        for (int w = 0; w < optSpec.Windows; w++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTime trainStart, trainEnd, testStart, testEnd;
            if (optSpec.WalkForwardMode == WalkForwardMode.Anchored)
            {
                trainStart = execSpec.StartDate;
                trainEnd = execSpec.StartDate.AddTicks(trainSize + w * testSize);
                testStart = trainEnd.AddTicks(1);
                testEnd = testStart.AddTicks(testSize);
            }
            else // Rolling
            {
                trainStart = execSpec.StartDate.AddTicks(w * windowSize);
                trainEnd = trainStart.AddTicks(trainSize);
                testStart = trainEnd.AddTicks(1);
                testEnd = trainStart.AddTicks(windowSize);
            }

            // Clamp to the overall end date for every window (not only the last)
            if (testEnd > execSpec.EndDate)
            {
                testEnd = execSpec.EndDate;
            }

            // Fetch data using BorrowedTickData (manual cleanup)
            var trainBorrowed = await DataFetchHelper.FetchAllAsync(
                    adapter, symbols, trainStart, trainEnd, execSpec.HistoricalDataPolicy, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var testBorrowed = await DataFetchHelper.FetchAllAsync(
                        adapter, symbols, testStart, testEnd, execSpec.HistoricalDataPolicy, cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    if (trainBorrowed.Streams.Length == 0 || testBorrowed.Streams.Length == 0)
                    {
                        continue;
                    }

                    // GA optimizer for this window
                    var optimizer = new GeneticOptimizer(
                        schema, optSpec.PopulationSize, optSpec.MasterSeed,
                        optSpec.MutationRate, optSpec.CrossoverRate, optSpec.ElitismPct, optSpec.TournamentSize,
                        optSpec.StagnationGenerationsBeforeHyper);

                    if (previousState is not null)
                    {
                        optimizer.LoadState(previousState);
                        optimizer.InvalidateFitness();
                    }
                    else
                    {
                        optimizer.Initialize();
                    }

                    async Task<double> Evaluator(Chromosome c, CancellationToken ct)
                    {
                        var input = new BacktestInput
                        {
                            TickStreams = trainBorrowed.Streams,
                            Symbols = trainBorrowed.Symbols,
                            Strategy = (IStrategy)Activator.CreateInstance(strategyType)!,
                            StrategySpecification = strategySpec with { InitialBalance = strategySpec.InitialBalance },
                            // No warm‑up for training – GA must see all data
                            ExecutionSpecification = execSpec with { WarmupWindowCount = 0 },
                            MarketCalculator = adapter.Calculator,
                            SymbolProperties = symbolProps,
                            Genes = c.Genes,
                            GeneInitializationSeed = execSpec.GeneInitializationSeed,
                            Progress = backtestProgress,
                            MessageBus = messageBus
                        };
                        var result = await runner.RunAsync(input, ct).ConfigureAwait(false);
                        return FitnessCalculator.Calculate(result, optSpec.FitnessModel,
                            strategySpec.InitialBalance);
                    }

                    double? prevBest = null;
                    for (int gen = 0; gen < optSpec.Generations; gen++)
                    {
                        await optimizer.EvaluateAsync(Evaluator, cancellationToken).ConfigureAwait(false);
                        optimizer.Evolve();

                        double best = optimizer.BestSolution.Fitness;
                        popHistory.Add(new GenerationSnapshot(
                            gen, best, optimizer.IsHyperMutation,
                            [
                                .. optimizer.Population.Select((c, i) =>
                                new IndividualSnapshot(i, SeedToolService.GetPortableDna(c.Genes), c.Fitness,
                                    c.Genes))
                            ]
                        ));

                        if (prevBest.HasValue)
                        {
                            ChronosMetrics.RecordGaFitnessImprovement(best - prevBest.Value);
                        }

                        optimizationProgress?.Report(new OptimizationProgress(gen, best, optimizer.IsHyperMutation));

                        prevBest = best;

                        messageBus?.Publish(new OptimizationGenerationEvent
                        {
                            Generation = gen,
                            BestFitness = best,
                            IsHyperMutation = optimizer.IsHyperMutation
                        });
                    }

                    // Save state for next window
                    finalState = optimizer.SaveState();
                    previousState = finalState;

                    // Out‑of‑sample test with best solution
                    var testInput = new BacktestInput
                    {
                        TickStreams = testBorrowed.Streams,
                        Symbols = testBorrowed.Symbols,
                        Strategy = (IStrategy)Activator.CreateInstance(strategyType)!,
                        StrategySpecification = strategySpec with { InitialBalance = stitchedBalance },
                        ExecutionSpecification = execSpec,
                        MarketCalculator = adapter.Calculator,
                        SymbolProperties = symbolProps,
                        Genes = optimizer.BestSolution.Genes,
                        GeneInitializationSeed = execSpec.GeneInitializationSeed,
                        Progress = backtestProgress,
                        MessageBus = messageBus
                    };
                    var oosResult = await runner.RunAsync(testInput, cancellationToken).ConfigureAwait(false);
                    allOosTrades.AddRange(oosResult.History);

                    foreach (var trade in oosResult.History)
                    {
                        stitchedBalance += trade.Profit;
                        if (stitchedBalance > peakBalance)
                        {
                            peakBalance = stitchedBalance;
                        }

                        double dd = (peakBalance - stitchedBalance) / peakBalance * 100.0;
                        if (dd > maxStitchedDD)
                        {
                            maxStitchedDD = dd;
                        }
                    }

                    windowSummaries.Add(new
                    {
                        Window = w + 1,
                        TrainStart = trainStart,
                        TrainEnd = trainEnd,
                        TestStart = testStart,
                        TestEnd = testEnd,
                        BestGenes = SeedToolService.GetPortableDna(optimizer.BestSolution.Genes)
                    });
                    messageBus?.Publish(new WalkForwardWindowCompletedEvent(w + 1, trainStart, trainEnd, testStart,
                        testEnd, optimizer.BestSolution.Fitness));
                }
                finally
                {
                    await testBorrowed.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await trainBorrowed.DisposeAsync().ConfigureAwait(false);
            }
        }

        totalSw.Stop();
        ChronosMetrics.RecordOptimizationDuration(totalSw.Elapsed.TotalSeconds);

        await adapter.DisconnectAsync().ConfigureAwait(false);

        // 4. Calculate stitched metrics
        var summary = metricsCalc.Calculate(
            new BacktestResult
            {
                Balance = stitchedBalance,
                Equity = stitchedBalance,
                Drawdown = maxStitchedDD,
                DailyDrawdown = maxStitchedDD, // No separate daily tracking in WFP; same value for now
                TotalTrades = allOosTrades.Count,
                History = allOosTrades
            },
            strategySpec.InitialBalance, execSpec.StartDate, execSpec.EndDate);
        var equityCurve = TradeAnalysisHelper.ReconstructCurve(allOosTrades, strategySpec.InitialBalance);
        var perSymbol = TradeAnalysisHelper.ComputePerSymbolMetrics(allOosTrades, strategySpec.InitialBalance);
        var correlation = TradeAnalysisHelper.ComputeCorrelationMatrix(allOosTrades, symbols);

        var reportData = new ReportData
        {
            Summary = summary,
            Trades = allOosTrades,
            EquityCurve = equityCurve,
            PopulationHistory = popHistory,
            PerSymbolMetrics = perSymbol,
            CorrelationMatrix = correlation
        };

        messageBus?.Publish(new BacktestCompletedEvent
        {
            NetProfit = summary.NetProfit,
            ReturnPct = summary.ReturnPct,
            MaxDrawdownPct = summary.MaxDrawdownPct,
            MaxDailyDrawdownPct = summary.MaxDailyDrawdownPct,
            TotalTrades = summary.TotalTrades,
            WinRatePct = summary.WinRatePct,
            ProfitFactor = summary.ProfitFactor,
            SharpeRatio = summary.SharpeRatio,
            SortinoRatio = summary.SortinoRatio
        });

        // Mark optimisation as completed for health check monitoring
        LiveMonitoringState.Instance.MarkOptimisationCompleted();
        LiveMonitoringState.Instance.MarkOptimisationStatus(true);

        var stream = await exporter.ExportAsync(reportData, cancellationToken).ConfigureAwait(false);
        return (stream, finalState);
    }
}
