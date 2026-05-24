using System.Diagnostics.Metrics;

namespace Chronos.Core.Kernel.Telemetry;

/// <summary>
/// Centralised factory for Chronos OpenTelemetry metrics.
/// Provides histograms and counters for GA, backtesting, and live trading.
/// </summary>
public static class ChronosMetrics
{
    // TODO: v3.0.0 – Refactor to instance‑based metrics and health checks for multi‑engine support.

    private static readonly Meter Meter = new("Chronos.Metrics", "1.0");

    private static readonly Histogram<double> GaFitnessImprovementHistogram =
        Meter.CreateHistogram<double>("chronos.ga.fitness_improvement",
            description: "Improvement in best fitness per generation");

    private static readonly Histogram<double> BacktestTicksPerSecondHistogram =
        Meter.CreateHistogram<double>("chronos.backtest.ticks_per_second", "ticks/s",
            "Rate of tick processing during backtest");

    private static readonly Histogram<double> LiveOrderLatencyHistogram =
        Meter.CreateHistogram<double>("chronos.live.order_latency_ms", "ms",
            "Latency of live order placement");

    private static readonly Counter<long> LiveOrderRejectionCounter =
        Meter.CreateCounter<long>("chronos.live.order_rejections_total",
            description: "Total live order rejections");

    private static readonly Histogram<double> OptimizationDurationHistogram =
        Meter.CreateHistogram<double>("chronos.optimization.duration_seconds", "s",
            "Duration of an optimization run");

    private static readonly Histogram<double> LiveTickLatencyHistogram =
        Meter.CreateHistogram<double>("chronos.live.tick_latency_ticks", "ticks", "Tick arrival latency");

    private static int _connectionState;
    private static string _adapterName = "unknown";

    // Phase 3: Connection status gauge
#pragma warning disable CA1823 // The gauge is registered with the Meter, not directly read
    private static readonly ObservableGauge<int> ConnectionStateGauge =
#pragma warning restore CA1823
        Meter.CreateObservableGauge(
            "chronos.live.connection_state",
            () => new Measurement<int>(_connectionState, new KeyValuePair<string, object?>("adapter", _adapterName)),
            description: "1 if connected, 0 if disconnected");

    /// <summary>Whether the live adapter is currently connected.</summary>
    public static bool IsConnected => _connectionState == 1;
    /// <summary>The name of the currently connected adapter.</summary>
    public static string AdapterName => _adapterName;

    /// <summary>Sets the connection state for health check and metrics.</summary>
    public static void SetConnectionState(bool connected, string adapterName)
    {
        _connectionState = connected ? 1 : 0;
        _adapterName = adapterName;
    }

    /// <summary>Records the latency (in 100‑ns ticks) between the tick timestamp and the current wall‑clock time.</summary>
    public static void RecordLiveTickLatency(long ticks) => LiveTickLatencyHistogram.Record(ticks);

    /// <summary>Records the improvement in best fitness between generations.</summary>
    public static void RecordGaFitnessImprovement(double improvement) =>
        GaFitnessImprovementHistogram.Record(improvement);

    /// <summary>Records the tick processing rate during a backtest.</summary>
    public static void RecordBacktestTicksPerSecond(double ticksPerSec) =>
        BacktestTicksPerSecondHistogram.Record(ticksPerSec);

    /// <summary>Records the latency of a live order placement.</summary>
    public static void RecordLiveOrderLatency(long milliseconds) =>
        LiveOrderLatencyHistogram.Record(milliseconds);

    /// <summary>Records a rejected live order.</summary>
    public static void RecordLiveOrderRejection() =>
        LiveOrderRejectionCounter.Add(1);

    /// <summary>Records the total duration of an optimisation run.</summary>
    public static void RecordOptimizationDuration(double seconds) =>
        OptimizationDurationHistogram.Record(seconds);
}
