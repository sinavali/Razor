using System.Diagnostics;
using Chronos.Abstractions.Shared;
using Chronos.Abstractions.Shared.Events;
using Chronos.Abstractions.Strategies;
using Chronos.Kernel.Brokers;
using Chronos.Kernel.Clock;
using Chronos.Kernel.Indicators;
using Chronos.Kernel.Metrics;
using Chronos.Kernel.Telemetry;

namespace Chronos.Kernel.Backtesting;

/// <summary>
/// Deterministic tick‑by‑tick backtest runner. Merges tick streams chronologically
/// and feeds them to the strategy. Uses <see cref="TickWindow"/> for strategies that
/// need aggregated statistics. Emits progress reports, a completed event with full
/// metrics, and records throughput telemetry.
/// </summary>
public sealed class BacktestRunner : IBacktestRunner
{
    private static long _eventCounter;

    /// <inheritdoc/>
    public Task<BacktestResult> RunAsync(BacktestInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        // ---------- setup ----------
        var clock = new TickClock();
        var broker = new SimulatedBroker(
            input.MarketCalculator,
            input.StrategySpecification.FrictionModel
                ?? throw new InvalidOperationException("Friction model required."),
            input.SymbolProperties,
            input.StrategySpecification.InitialBalance,
            input.StrategySpecification.Leverage,
            clock,
            input.ExecutionSpecification.LatencyTicks,
            input.ExecutionSpecification.MaxOpenPositions,
            input.ExecutionSpecification.StopOutLevel,
            input.MessageBus);

        var symbols = input.Symbols;
        var timeframes = input.StrategySpecification.RequestedSymbols
            .SelectMany(r => r.TimeFrames)
            .Distinct()
            .Where(tf => tf != TimeFrame.Tick)
            .ToList();
        using var tickWindow = new TickWindow(symbols, timeframes);

        if (input.Strategy is StrategyBase sb)
        {
            sb.WireUp(broker, tickWindow);
        }

        double[] genes = input.Genes
            ?? GeneInjector.ExtractAndInitializeGenes(
                input.Strategy,
                input.NeuralNetwork,
                input.GeneInitializationSeed);
        input.Strategy.InjectGenes(genes);

        // ---------- initialise strategy ----------
#pragma warning disable CA1849 // Sync-over-async intentional for deterministic loop
        input.Strategy.OnConfigureAsync(input.StrategySpecification).GetAwaiter().GetResult();
        var indicatorRegistry = IndicatorRegistryFactory.Create();
        input.Strategy.OnStartAsync(indicatorRegistry).GetAwaiter().GetResult();
#pragma warning restore CA1849

        // ---------- publish started event ----------
        input.MessageBus?.Publish(new BacktestStartedEvent());

        // ---------- main merge & processing loop ----------
        var merged = MergedTickTimeline.EnumerateEvents(input.TickStreams, symbols);
        long processed = 0;
        long totalEvents = input.TickStreams.Sum(s => (long)s.Count);
        var lastProgress = -1;

        // Start the wall clock for throughput measurement.
        var wallClock = Stopwatch.StartNew();

        try
        {
            foreach (var (time, streamIdx, tick) in merged)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string sym = symbols[streamIdx];

#pragma warning disable CA1849
                broker.OnTickAsync(sym, tick).GetAwaiter().GetResult();
                tickWindow.PushTick(sym, tick);
                input.Strategy.OnTickAsync(tick).GetAwaiter().GetResult();
#pragma warning restore CA1849

                processed++;
                int pct = (int)(processed * 100 / totalEvents);
                if (pct > lastProgress)
                {
                    lastProgress = pct;
                    input.Progress?.Report(
                        new BacktestProgress(pct, $"Processing tick {processed}/{totalEvents}"));
                }
            }
        }
        finally
        {
            // ---------- teardown (always runs) ----------
            foreach (var sym in symbols)
#pragma warning disable CA1849
                broker.CloseAllAsync(sym).GetAwaiter().GetResult();
#pragma warning restore CA1849
            indicatorRegistry.DisposeAll();
#pragma warning disable CA1849
            input.Strategy.OnStopAsync().GetAwaiter().GetResult();
#pragma warning restore CA1849
            tickWindow.Dispose();
        }

        wallClock.Stop();

        // ---------- final progress ----------
        input.Progress?.Report(new BacktestProgress(100, "Done"));

        // ---------- assemble result ----------
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

        // ---------- publish completed event with full metrics ----------
        var metricsCalc = new MetricsCalculator();
        var summary = metricsCalc.Calculate(
            result,
            input.StrategySpecification.InitialBalance,
            input.ExecutionSpecification.StartDate,
            input.ExecutionSpecification.EndDate);

        input.MessageBus?.Publish(new BacktestCompletedEvent
        {
            NetProfit = summary.NetProfit,
            ReturnPct = summary.ReturnPct,
            MaxDrawdownPct = summary.MaxDrawdownPct,
            MaxDailyDrawdownPct = summary.MaxDailyDrawdownPct,
            TotalTrades = summary.TotalTrades,
            WinRatePct = summary.WinRatePct,
            ProfitFactor = summary.ProfitFactor,
            SharpeRatio = summary.SharpeRatio,
            SortinoRatio = summary.SortinoRatio,
            EventId = $"bt-{Interlocked.Increment(ref _eventCounter)}",
            CorrelationId = null
        });

        // ---------- record throughput telemetry ----------
        double elapsedSeconds = wallClock.Elapsed.TotalSeconds;
        if (elapsedSeconds > 0.0)
        {
            double ticksPerSec = totalEvents / elapsedSeconds;
            ChronosMetrics.RecordBacktestTicksPerSecond(ticksPerSec);
        }

        return Task.FromResult(result);
    }
}