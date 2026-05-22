using Chronos.Core.Adapters;
using Chronos.Core.Configuration;
using Chronos.Core.Neural;
using Chronos.Core.Strategies;
using Chronos.Core.Trading;
using Chronos.Messaging;
using Chronos.Messaging.Events;
using Chronos.Orchestration.Reporting;
using Chronos.Orchestration.Utils;
using Chronos.Sdk.Backtesting;
using Chronos.Sdk.Data;
using Chronos.Sdk.Metrics;
using Chronos.Sdk.Telemetry;
using System.Diagnostics;

namespace Chronos.Orchestration.Pipelines;

/// <summary>
/// Recipe for a single backtest run: fetch data → run backtest → produce report.
/// </summary>
public static class BacktestPipeline
{
    /// <summary>
    /// Executes a backtest and exports the report to a stream.
    /// </summary>
    public static async Task<Stream> ExecuteAsync(
        IAdapter adapter,
        IStrategy strategy,
        StrategySpecification strategySpec,
        ExecutionSpecification execSpec,
        IBacktestRunner runner,
        IMetricsCalculator metricsCalc,
        IReportExporter exporter,
        double[]? genes = null,
        FeedForwardNetwork? neuralNetwork = null,
        IMessageBus? messageBus = null,
        IProgress<BacktestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(strategySpec);
        ArgumentNullException.ThrowIfNull(execSpec);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(metricsCalc);
        ArgumentNullException.ThrowIfNull(exporter);

        await adapter.ConnectAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, SymbolProperties> specs = [];
        foreach (var req in strategySpec.RequestedSymbols)
        {
            var props = await adapter.GetSymbolPropertiesAsync(req.Symbol, cancellationToken).ConfigureAwait(false);
            specs[req.Symbol] = props ?? new SymbolProperties
            {
                ContractSize = 1,
                TickSize = 0.0001,
                TickValue = 0.001,
                MinVolume = 0.0001,
                MaxLeverage = 100
            };

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

        BorrowedTickData borrowed = await DataFetchHelper.FetchAllAsync(
            adapter,
            strategySpec.RequestedSymbols.Select(r => r.Symbol),
            execSpec.StartDate,
            execSpec.EndDate,
            execSpec.HistoricalDataPolicy,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await adapter.DisconnectAsync().ConfigureAwait(false);

            if (borrowed.Streams.Length == 0)
            {
                throw new InvalidOperationException("No historical data available for the requested symbols and range.");
            }

            Stopwatch sw = Stopwatch.StartNew();
            var input = new BacktestInput
            {
                TickStreams = borrowed.Streams,
                Symbols = borrowed.Symbols,
                Strategy = strategy,
                StrategySpecification = strategySpec,
                ExecutionSpecification = execSpec,
                MarketCalculator = adapter.Calculator,
                SymbolProperties = specs,
                Genes = genes,
                NeuralNetwork = neuralNetwork,
                GeneInitializationSeed = execSpec.GeneInitializationSeed,
                Progress = progress,
                MessageBus = messageBus
            };

            var result = await runner.RunAsync(input, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (result.TotalTrades > 0 || input.TickStreams.Length > 0)
            {
                ChronosMetrics.RecordBacktestTicksPerSecond(input.TickStreams.Sum(s => s.Count) / sw.Elapsed.TotalSeconds);
            }

            // Calculate summary metrics before publishing the event
            var summary = metricsCalc.Calculate(result, strategySpec.InitialBalance, execSpec.StartDate, execSpec.EndDate);

            // Publish enriched event with full metrics
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

            var equityCurve = TradeAnalysisHelper.ReconstructCurve(result.History, strategySpec.InitialBalance);
            var perSymbol = TradeAnalysisHelper.ComputePerSymbolMetrics(result.History, strategySpec.InitialBalance);
            var correlation = TradeAnalysisHelper.ComputeCorrelationMatrix(result.History, borrowed.Symbols);

            var reportData = new ReportData
            {
                Summary = summary,
                Trades = result.History,
                EquityCurve = equityCurve,
                PerSymbolMetrics = perSymbol,
                CorrelationMatrix = correlation
            };
            return await exporter.ExportAsync(reportData, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await borrowed.DisposeAsync().ConfigureAwait(false);
        }
    }
}
