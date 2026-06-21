namespace Chronos.Core.Engine.Management.Tasks;

/// <summary>Task states.</summary>
internal enum TaskState
{
    Initializing,
    Running,
    Paused,
    Completed,
    Canceled,
    Faulted
}

/// <summary>Represents an engine task.</summary>
internal abstract class EngineTask
{
    public string TaskId { get; protected set; } = string.Empty;
    public string TaskType { get; protected set; } = string.Empty;
    public TaskState State { get; internal set; } = TaskState.Initializing;
    public DateTime StartTime { get; protected set; }
    public DateTime? EndTime { get; internal set; }
    public CancellationTokenSource CancellationTokenSource { get; internal set; } = new();

    public abstract Task ExecuteAsync(CancellationToken cancellationToken);

    public virtual Task PauseAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Paused;
        return Task.CompletedTask;
    }

    public virtual Task ResumeAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Running;
        return Task.CompletedTask;
    }

    public virtual Task<object> GetStateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new { TaskId, TaskType, State = State.ToString(), StartTime, EndTime });
    }
}

/// <summary>Manages all engine tasks.</summary>
internal interface ITaskManager
{
    IReadOnlyList<EngineTask> RunningTasks { get; }
    IReadOnlyList<EngineTask> AllTasks { get; }

    EngineTask? GetTask(string taskId);
    Task<string> StartLiveTaskAsync(object config, CancellationToken cancellationToken);
    Task StopLiveTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<string> StartBacktestTaskAsync(object input, CancellationToken cancellationToken);
    Task CancelBacktestTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<string> StartOptimizationTaskAsync(object config, CancellationToken cancellationToken);
    Task CancelOptimizationTaskAsync(string taskId, CancellationToken cancellationToken);
    Task PauseTaskAsync(string taskId, CancellationToken cancellationToken);
    Task ResumeTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<object> GetTaskStateAsync(string taskId, CancellationToken cancellationToken);
    Task<object> GetOptimizationResultAsync(string taskId, CancellationToken cancellationToken);
    Task StopAllTasksAsync(CancellationToken cancellationToken);
    Task StopAllUserTasksAsync(CancellationToken cancellationToken);
    Task InjectGenesAsync(string taskId, double[] genes, CancellationToken cancellationToken);
    Task<object> GetLiveStateAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Stops all live trading tasks gracefully.</summary>
    Task StopAllLiveTasksAsync(CancellationToken cancellationToken);
}
