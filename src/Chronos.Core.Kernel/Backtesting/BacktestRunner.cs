using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Chronos.Core.Kernel.Brokers;
using Chronos.Core.Kernel.Clock;
using Chronos.Core.Kernel.Events;
using Chronos.Core.Kernel.Hooks;
using Chronos.Core.Kernel.Indicators;
using Chronos.Core.Kernel.Metrics;
using Chronos.Core.Kernel.Telemetry;
using System.Diagnostics;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>
/// Deterministic tick‑by‑tick backtest runner with full hook pipeline integration.
/// </summary>
public sealed class BacktestRunner : IBacktestRunner
{
    private static long _eventCounter;

    /// <inheritdoc />
    public Task<BacktestResult> RunAsync(BacktestInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => RunInternal(input, cancellationToken), cancellationToken);
    }

    private BacktestResult RunInternal(BacktestInput input, CancellationToken ct)
    {
        var clock = new TickClock();

        // Handle metrics ownership without CA2000 and CS8600
        CoreMetrics? ownedMetrics = input.Metrics is null ? new CoreMetrics("backtest-runner-internal") : null;
        ICoreMetrics metrics = ownedMetrics ?? input.Metrics!;

        try
        {
            var hooks = input.HookRegistry?.Backtest as BacktestHooks;

            var broker = new SimulatedBroker(
                input.MarketCalculator,
                input.SymbolProperties,
                input.StrategySpecification.InitialBalance,
                input.StrategySpecification.Leverage,
                clock,
                input.ExecutionSpecification.LatencyTicks,
                input.ExecutionSpecification.MaxOpenPositions,
                input.ExecutionSpecification.StopOutLevel,
                input.MessageBus,
                hooks,
                input.CurrencyConverter,
                input.AccountCurrency,
                null); // logger not needed in backtest

            var timeframes = input.StrategySpecification.RequestedSymbols
                .SelectMany(r => r.TimeFrames)
                .Distinct()
                .Where(tf => tf != TimeFrame.Tick)
                .ToList();

            var tickWindow = new TickWindow(input.Symbols, timeframes);

            int warmupRemaining = input.ExecutionSpecification.WarmupWindowCount;
            broker.IsWarmup = warmupRemaining > 0;

            // If no non‑Tick timeframes, warm‑up is effectively immediate
            if (timeframes.Count == 0)
            {
                warmupRemaining = 0;
                broker.IsWarmup = false;
            }

            void DecrementWarmup(string _, TimeFrame __)
            {
                if (broker.IsWarmup)
                {
                    warmupRemaining--;
                    if (warmupRemaining <= 0)
                    {
                        broker.IsWarmup = false;
                        tickWindow.WindowCompleted -= DecrementWarmup;
                    }
                }
            }

            if (broker.IsWarmup)
            {
                tickWindow.WindowCompleted += DecrementWarmup;
            }

            // Forward window completion synchronously (no Task.Run)
            void ForwardWindowCompletion(string symbol, TimeFrame tf)
            {
                if (input.Strategy is StrategyBase sb)
                {
                    // Execute synchronously to preserve determinism
                    sb.NotifyWindowCompletedAsync(symbol, tf).GetAwaiter().GetResult();
                }
            }

            if (timeframes.Count > 0 && input.Strategy is StrategyBase)
            {
                tickWindow.WindowCompleted += ForwardWindowCompletion;
            }

            try
            {
                if (input.Strategy is StrategyBase sb)
                {
                    sb.WireUp(broker, tickWindow);
                }

                // If a neural network is provided, set it on the strategy
                if (input.NeuralNetwork != null && input.Strategy is IStrategyCapability strategy)
                {
                    strategy.NeuralNetwork = input.NeuralNetwork;
                }

                double[] genes;
                if (input.Genes is not null)
                {
                    genes = input.Genes;
                }
                else if (input.GeneInitializationSeed.HasValue)
                {
                    genes = GeneInjector.ExtractAndInitializeGenes(
                        input.Strategy, input.NeuralNetwork, input.GeneInitializationSeed.Value);
                }
                else
                {
                    genes = input.Strategy.ExportGenes();
                }

                input.Strategy.InjectGenes(genes);

                input.Strategy.OnConfigureAsync(input.StrategySpecification).GetAwaiter().GetResult();
                var indicatorRegistry = IndicatorRegistryFactory.Create(tickWindow);
                input.Strategy.OnStartAsync(indicatorRegistry).GetAwaiter().GetResult();

                // backtest.started hook – pass the cancellation token
                hooks?.OnStart.InvokeActionChain(
                    new BacktestContext(clock, new Tick(), 0, 0, broker.Equity, broker.Balance, 0, broker, tickWindow,
                        Array.Empty<Position>(), "backtest.started", ct));

                input.MessageBus?.Publish(new BacktestStartedEvent { Timestamp = clock.GetUtcNow() });

                var merged = MergedTickTimeline.EnumerateEvents(input.TickStreams, input.Symbols);
                long processed = 0;
                long totalEvents = input.TickStreams.Sum(s => (long)(s?.Count ?? 0));
                int lastProgress = -1;
                var wallClock = Stopwatch.StartNew();

                foreach (var (_, streamIdx, tick) in merged)
                {
                    ct.ThrowIfCancellationRequested();
                    string sym = input.Symbols[streamIdx];
                    clock.SetTickTime(tick.Time);

                    Tick currentTick = tick;

                    // backtest.tick.received filter – pass ct
                    if (hooks?.OnTickReceived is not null)
                    {
                        var ctx = new BacktestContext(clock, currentTick, processed, totalEvents,
                            broker.Equity, broker.Balance, broker.MaxDrawdown, broker, tickWindow,
                            broker.GetOpenPositionsAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult(),
                            "backtest.tick.received", ct);
                        var filterResult = hooks.OnTickReceived.InvokeFilterChain(currentTick, ctx);
                        if (!filterResult.IsAllowed)
                        {
                            continue;
                        }

                        currentTick = filterResult.IsAllowed ? filterResult.Data : currentTick;
                    }

                    broker.OnTickAsync(sym, currentTick).GetAwaiter().GetResult();

                    // backtest.tick.strategy_before filter – pass ct
                    if (hooks?.OnTickStrategyBefore is not null)
                    {
                        var ctx = new BacktestContext(clock, currentTick, processed, totalEvents,
                            broker.Equity, broker.Balance, broker.MaxDrawdown, broker, tickWindow,
                            broker.GetOpenPositionsAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult(),
                            "backtest.tick.strategy_before", ct);
                        var filterResult = hooks.OnTickStrategyBefore.InvokeFilterChain(currentTick, ctx);
                        if (!filterResult.IsAllowed)
                        {
                            goto AfterTick;
                        }

                        currentTick = filterResult.IsAllowed ? filterResult.Data : currentTick;
                    }

                    tickWindow.PushTick(sym, currentTick);
                    input.Strategy.OnTick(sym, currentTick);

                    // backtest.tick.strategy_after action – pass ct
                    hooks?.OnTickStrategyAfter.InvokeActionChain(currentTick,
                        new BacktestContext(clock, currentTick, processed, totalEvents,
                            broker.Equity, broker.Balance, broker.MaxDrawdown, broker, tickWindow,
                            broker.GetOpenPositionsAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult(),
                            "backtest.tick.strategy_after", ct));

                AfterTick:
                    // backtest.tick.completed action – pass ct
                    hooks?.OnTickCompleted.InvokeActionChain(currentTick,
                        new BacktestContext(clock, currentTick, processed, totalEvents,
                            broker.Equity, broker.Balance, broker.MaxDrawdown, broker, tickWindow,
                            broker.GetOpenPositionsAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult(),
                            "backtest.tick.completed", ct));

                    processed++;
                    int pct = totalEvents > 0 ? (int)(processed * 100 / totalEvents) : 100;
                    if (pct > lastProgress)
                    {
                        lastProgress = pct;
                        input.Progress?.Report(new BacktestProgress(pct, $"Processing tick {processed}/{totalEvents}"));
                    }
                }

                wallClock.Stop();
                input.Progress?.Report(new BacktestProgress(100, "Done"));

                foreach (var sym in input.Symbols)
                {
                    broker.CloseAllAsync(sym).GetAwaiter().GetResult();
                }

                indicatorRegistry.DisposeAll();
                input.Strategy.OnStopAsync().GetAwaiter().GetResult();

                var history = broker.GetHistoryAsync(CancellationToken.None).GetAwaiter().GetResult();
                var result = new BacktestResult
                {
                    Balance = broker.Balance,
                    Equity = broker.Equity,
                    Drawdown = broker.MaxDrawdown,
                    DailyDrawdown = broker.MaxDailyDrawdown,
                    TotalTrades = history.Count,
                    History = history
                };

                var metricsCalc = new MetricsCalculator();
                var summary = metricsCalc.Calculate(result, input.StrategySpecification.InitialBalance,
                    input.ExecutionSpecification.StartDate, input.ExecutionSpecification.EndDate);

                input.MessageBus?.Publish(new BacktestCompletedEvent
                {
                    Timestamp = clock.GetUtcNow(),
                    NetProfit = summary.NetProfit,
                    ReturnPct = summary.ReturnPct,
                    MaxDrawdownPct = summary.MaxDrawdownPct,
                    MaxDailyDrawdownPct = summary.MaxDailyDrawdownPct,
                    TotalTrades = summary.TotalTrades,
                    WinRatePct = summary.WinRatePct,
                    ProfitFactor = summary.ProfitFactor,
                    SharpeRatio = summary.SharpeRatio,
                    SortinoRatio = summary.SortinoRatio,
                    EventId = $"bt-{Interlocked.Increment(ref _eventCounter)}"
                });

                // IMP-02: Removed ReportGenerator usage – report rendering is handled by Cloud.

                // backtest.completed hook – pass ct
                hooks?.OnCompleted.InvokeActionChain(
                    new BacktestContext(clock, new Tick(), processed, totalEvents,
                        broker.Equity, broker.Balance, broker.MaxDrawdown, broker, tickWindow,
                        broker.GetOpenPositionsAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult(),
                        "backtest.completed", ct));

                double elapsedSeconds = wallClock.Elapsed.TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    metrics.RecordBacktestTicksPerSecond(totalEvents / elapsedSeconds);
                }

                return result;
            }
            finally
            {
                tickWindow.WindowCompleted -= DecrementWarmup;
                if (timeframes.Count > 0 && input.Strategy is StrategyBase)
                {
                    tickWindow.WindowCompleted -= ForwardWindowCompletion;
                }

                tickWindow.Dispose();
            }
        }
        finally
        {
            ownedMetrics?.Dispose();
        }
    }
}
