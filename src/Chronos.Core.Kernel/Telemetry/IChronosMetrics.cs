namespace Chronos.Core.Kernel.Telemetry;

/// <summary>
/// Interface for telemetry recording (OpenTelemetry abstraction).
/// Extracted to allow multi-engine instances to separate their metrics via tags.
/// </summary>
public interface IChronosMetrics
{
    /// <summary>Whether the live adapter is currently connected.</summary>
    bool IsConnected { get; }

    /// <summary>The name of the currently connected adapter.</summary>
    string AdapterName { get; }

    /// <summary>Sets the connection state for health check and metrics.</summary>
    void SetConnectionState(bool connected, string adapterName);

    /// <summary>Records the latency (in 100‑ns ticks) between the tick timestamp and the current wall‑clock time.</summary>
    void RecordLiveTickLatency(long ticks);

    /// <summary>Records the improvement in best fitness between generations.</summary>
    void RecordGaFitnessImprovement(double improvement);

    /// <summary>Records the tick processing rate during a backtest.</summary>
    void RecordBacktestTicksPerSecond(double ticksPerSec);

    /// <summary>Records the latency of a live order placement.</summary>
    void RecordLiveOrderLatency(long milliseconds);

    /// <summary>Records a rejected live order.</summary>
    void RecordLiveOrderRejection();

    /// <summary>Records the total duration of an optimisation run.</summary>
    void RecordOptimizationDuration(double seconds);
}
