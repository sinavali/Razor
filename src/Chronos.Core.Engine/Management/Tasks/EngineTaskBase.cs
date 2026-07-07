namespace Chronos.Core.Engine.Management.Tasks;

/// <summary>Represents an engine task.</summary>
internal abstract class EngineTaskBase : IDisposable
{
    /// <summary>Gets the task identifier.</summary>
    public string TaskId { get; protected set; } = string.Empty;
    /// <summary>Gets the task type (Live, Backtest, Optimization).</summary>
    public string TaskType { get; protected set; } = string.Empty;
    /// <summary>Gets or sets the current state.</summary>
    public TaskState State { get; internal set; } = TaskState.Initializing;
    /// <summary>Gets or sets the start time.</summary>
    public DateTime StartTime { get; set; }
    /// <summary>Gets or sets the end time.</summary>
    public DateTime? EndTime { get; internal set; }
    /// <summary>Gets the cancellation token source.</summary>
    public CancellationTokenSource CancellationTokenSource { get; internal set; } = new();

    /// <summary>Executes the task asynchronously.</summary>
    public abstract Task ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>Pauses the task.</summary>
    public virtual Task PauseAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Paused;
        return Task.CompletedTask;
    }

    /// <summary>Resumes the task.</summary>
    public virtual Task ResumeAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Running;
        return Task.CompletedTask;
    }

    /// <summary>Gets the task state as an object.</summary>
    public virtual Task<object> GetStateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new { TaskId, TaskType, State = State.ToString(), StartTime, EndTime });
    }

    /// <summary>Disposes the cancellation token source.</summary>
    public void Dispose()
    {
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
