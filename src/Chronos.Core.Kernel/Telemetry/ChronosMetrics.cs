using System.Diagnostics.Metrics;
using Chronos.Core.Abstractions.Telemetry;

namespace Chronos.Core.Kernel.Telemetry;

/// <summary>
/// Centralised factory for Chronos OpenTelemetry metrics.
/// Provides histograms and counters for GA, backtesting, and live trading.
/// Instance-based tracking for multi-engine deployments.
/// </summary>
public sealed class ChronosMetrics : IChronosMetrics
{
    private static readonly Meter Meter = new("Chronos.Metrics", "1.0");

    private static readonly Histogram<double> GaFitnessImprovementHistogram =
        Meter.CreateHistogram<double>("chronos.ga.fitness_improvement", description: "Improvement in best fitness per generation");

    private static readonly Histogram<double> BacktestTicksPerSecondHistogram =
        Meter.CreateHistogram<double>("chronos.backtest.ticks_per_second", "ticks/s", "Rate of tick processing during backtest");

    private static readonly Histogram<double> LiveOrderLatencyHistogram =
        Meter.CreateHistogram<double>("chronos.live.order_latency_ms", "ms", "Latency of live order placement");

    private static readonly Counter<long> LiveOrderRejectionCounter =
        Meter.CreateCounter<long>("chronos.live.order_rejections_total", description: "Total live order rejections");

    private static readonly Histogram<double> OptimizationDurationHistogram =
        Meter.CreateHistogram<double>("chronos.optimization.duration_seconds", "s", "Duration of an optimization run");

    private static readonly Histogram<double> LiveTickLatencyHistogram =
        Meter.CreateHistogram<double>("chronos.live.tick_latency_ticks", "ticks", "Tick arrival latency");

    private bool _isConnected;
    private string _adapterName = "unknown";
    private readonly KeyValuePair<string, object?> _instanceTag;

    /// <inheritdoc />
    public bool IsConnected => _isConnected;

    /// <inheritdoc />
    public string AdapterName => _adapterName;

    /// <summary>Initializes a new instance of the metrics tracker.</summary>
    public ChronosMetrics(string instanceId)
    {
        _instanceTag = new KeyValuePair<string, object?>("instance_id", instanceId);

        Meter.CreateObservableGauge(
            "chronos.live.connection_state",
            () => new Measurement<int>(_isConnected ? 1 : 0, _instanceTag),
            description: "1 if connected, 0 if disconnected");
    }

    /// <inheritdoc />
    public void SetConnectionState(bool connected, string adapterName)
    {
        _isConnected = connected;
        _adapterName = adapterName;
    }

    /// <inheritdoc />
    public void RecordLiveTickLatency(long ticks) => LiveTickLatencyHistogram.Record(ticks, _instanceTag);

    /// <inheritdoc />
    public void RecordGaFitnessImprovement(double improvement) => GaFitnessImprovementHistogram.Record(improvement, _instanceTag);

    /// <inheritdoc />
    public void RecordBacktestTicksPerSecond(double ticksPerSec) => BacktestTicksPerSecondHistogram.Record(ticksPerSec, _instanceTag);

    /// <inheritdoc />
    public void RecordLiveOrderLatency(long milliseconds) => LiveOrderLatencyHistogram.Record(milliseconds, _instanceTag);

    /// <inheritdoc />
    public void RecordLiveOrderRejection() => LiveOrderRejectionCounter.Add(1, _instanceTag);

    /// <inheritdoc />
    public void RecordOptimizationDuration(double seconds) => OptimizationDurationHistogram.Record(seconds, _instanceTag);
}
