using System.Diagnostics.Metrics;

namespace Chronos.Core.Engine.Core;

/// <summary>Engine‑specific telemetry metrics.</summary>
internal interface IEngineTelemetry
{
    void SetConnectionState(bool isConnected);
    void RecordStartup();
    void RecordCommandExecution(int commandId, long durationMs);
    void RecordTaskStart(string taskType);
    void RecordTaskCompletion(string taskType, bool success);
    void RecordLiveTickAge(long ageTicks);
    /// <summary>Gets a snapshot of current telemetry data.</summary>
    object GetMetricsSnapshot();
}

/// <summary>Default implementation using System.Diagnostics.Metrics.</summary>
internal sealed class EngineTelemetry : IEngineTelemetry, IDisposable
{
    private static readonly Meter Meter = new("Chronos.Core.Engine", "1.0");
    private static readonly Counter<long> CommandCounter = Meter.CreateCounter<long>("engine.commands_total", description: "Total commands executed.");
    private static readonly Histogram<double> CommandDurationHistogram = Meter.CreateHistogram<double>("engine.command_duration_ms", "ms", "Command execution duration.");
    private static readonly Counter<long> TaskCounter = Meter.CreateCounter<long>("engine.tasks_total", description: "Total tasks started.");
    private static readonly Histogram<double> TaskDurationHistogram = Meter.CreateHistogram<double>("engine.task_duration_ms", "ms", "Task execution duration.");
    private static readonly Histogram<double> LiveTickAgeHistogram = Meter.CreateHistogram<double>("engine.live_tick_age_ticks", "ticks", "Age of the last received live tick in 100ns ticks.");

    private bool _isConnected;
    private long _totalCommands;
    private long _totalTasks;
    private bool _disposed;

    // Observable gauge for connection state – static to share across instances
    private static readonly List<WeakReference<EngineTelemetry>> _instances = new();
    private static readonly Lock _instancesLock = new();

    static EngineTelemetry()
    {
        Meter.CreateObservableGauge(
            "engine.connection_state",
            () =>
            {
                var measurements = new List<Measurement<int>>();
                lock (_instancesLock)
                {
                    for (int i = _instances.Count - 1; i >= 0; i--)
                    {
                        if (_instances[i].TryGetTarget(out var inst))
                        {
                            measurements.Add(new Measurement<int>(inst._isConnected ? 1 : 0));
                        }
                        else
                        {
                            _instances.RemoveAt(i);
                        }
                    }
                }
                return measurements;
            },
            description: "1 if connected to Cloud, 0 otherwise");
    }

    public EngineTelemetry()
    {
        lock (_instancesLock)
        {
            _instances.Add(new WeakReference<EngineTelemetry>(this));
        }
    }

    /// <inheritdoc/>
    public void SetConnectionState(bool isConnected) => _isConnected = isConnected;

    /// <inheritdoc/>
    public void RecordStartup() { }

    /// <inheritdoc/>
    public void RecordCommandExecution(int commandId, long durationMs)
    {
        _totalCommands++;
        CommandCounter.Add(1, new KeyValuePair<string, object?>("command_id", commandId));
        CommandDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("command_id", commandId));
    }

    /// <inheritdoc/>
    public void RecordTaskStart(string taskType)
    {
        _totalTasks++;
        TaskCounter.Add(1, new KeyValuePair<string, object?>("task_type", taskType));
    }

    /// <inheritdoc/>
    public void RecordTaskCompletion(string taskType, bool success)
    {
        TaskDurationHistogram.Record(0, new KeyValuePair<string, object?>("task_type", taskType), new KeyValuePair<string, object?>("success", success));
    }

    /// <inheritdoc/>
    public void RecordLiveTickAge(long ageTicks)
    {
        LiveTickAgeHistogram.Record(ageTicks);
    }

    /// <inheritdoc/>
    public object GetMetricsSnapshot()
    {
        return new
        {
            IsConnected = _isConnected,
            TotalCommandsExecuted = _totalCommands,
            TotalTasksStarted = _totalTasks,
            TimestampUtc = DateTime.UtcNow
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
