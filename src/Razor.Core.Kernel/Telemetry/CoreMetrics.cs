using System.Diagnostics.Metrics;

namespace Razor.Core.Kernel.Telemetry;

/// <summary>
/// Centralised factory for core telemetry metrics.
/// Provides histograms and counters for GA, backtesting, and live trading.
/// Instance‑based tracking for multi‑engine deployments.
/// </summary>
public sealed class CoreMetrics : ICoreMetrics, IDisposable
{
    private static readonly Meter Meter = new("Core.Metrics", "1.0");

    private static readonly Histogram<double> GaFitnessImprovementHistogram =
        Meter.CreateHistogram<double>("core.ga.fitness_improvement", description: "Improvement in best fitness per generation");

    private static readonly Histogram<double> BacktestTicksPerSecondHistogram =
        Meter.CreateHistogram<double>("core.backtest.ticks_per_second", "ticks/s", "Rate of tick processing during backtest");

    private static readonly Histogram<double> LiveOrderLatencyHistogram =
        Meter.CreateHistogram<double>("core.live.order_latency_ms", "ms", "Latency of live order placement");

    private static readonly Counter<long> LiveOrderRejectionCounter =
        Meter.CreateCounter<long>("core.live.order_rejections_total", description: "Total live order rejections");

    private static readonly Histogram<double> OptimizationDurationHistogram =
        Meter.CreateHistogram<double>("core.optimization.duration_seconds", "s", "Duration of an optimization run");

    private static readonly Histogram<double> LiveTickLatencyHistogram =
        Meter.CreateHistogram<double>("core.live.tick_latency_ticks", "ticks", "Tick arrival latency");

    private static readonly List<WeakReference<CoreMetrics>> _instances = new();
    private static readonly Lock _instancesLock = new();
    private static bool _gaugeRegistered;

    private bool _isConnected;
    private string _adapterName = "unknown";
    private readonly KeyValuePair<string, object?> _instanceTag;

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public string AdapterName => _adapterName;

    /// <summary>Initializes a new instance of the metrics tracker.</summary>
    public CoreMetrics(string instanceId)
    {
        _instanceTag = new KeyValuePair<string, object?>("instance_id", instanceId);

        lock (_instancesLock)
        {
            _instances.Add(new WeakReference<CoreMetrics>(this));
            if (!_gaugeRegistered)
            {
                Meter.CreateObservableGauge(
                    "core.live.connection_state",
                    () =>
                    {
                        List<Measurement<int>> measurements = [];
                        lock (_instancesLock)
                        {
                            for (int i = _instances.Count - 1; i >= 0; i--)
                            {
                                if (_instances[i].TryGetTarget(out var inst))
                                {
                                    measurements.Add(new Measurement<int>(
                                        inst._isConnected ? 1 : 0,
                                        inst._instanceTag));
                                }
                                else
                                {
                                    _instances.RemoveAt(i);
                                }
                            }
                        }

                        return measurements;
                    },
                    description: "1 if connected, 0 if disconnected");
                _gaugeRegistered = true;
            }
        }
    }

    /// <inheritdoc/>
    public void SetConnectionState(bool connected, string adapterName)
    {
        _isConnected = connected;
        _adapterName = adapterName;
    }

    /// <inheritdoc/>
    public void RecordLiveTickLatency(long ticks) => LiveTickLatencyHistogram.Record(ticks, _instanceTag);

    /// <inheritdoc/>
    public void RecordGaFitnessImprovement(double improvement) => GaFitnessImprovementHistogram.Record(improvement, _instanceTag);

    /// <inheritdoc/>
    public void RecordBacktestTicksPerSecond(double ticksPerSec) => BacktestTicksPerSecondHistogram.Record(ticksPerSec, _instanceTag);

    /// <inheritdoc/>
    public void RecordLiveOrderLatency(long milliseconds) => LiveOrderLatencyHistogram.Record(milliseconds, _instanceTag);

    /// <inheritdoc/>
    public void RecordLiveOrderRejection() => LiveOrderRejectionCounter.Add(1, _instanceTag);

    /// <inheritdoc/>
    public void RecordOptimizationDuration(double seconds) => OptimizationDurationHistogram.Record(seconds, _instanceTag);

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_instancesLock)
        {
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (_instances[i].TryGetTarget(out var inst) && ReferenceEquals(inst, this))
                {
                    _instances.RemoveAt(i);
                }
            }
        }
    }
}
