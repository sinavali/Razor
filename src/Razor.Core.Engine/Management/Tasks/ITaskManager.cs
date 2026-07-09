using Razor.Core.Engine.Core;

namespace Razor.Core.Engine.Management.Tasks;

/// <summary>Task states.</summary>
internal enum TaskState
{
    /// <summary>Task is initialising.</summary>
    Initializing,
    /// <summary>Task is running.</summary>
    Running,
    /// <summary>Task is paused.</summary>
    Paused,
    /// <summary>Task has completed successfully.</summary>
    Completed,
    /// <summary>Task was cancelled.</summary>
    Canceled,
    /// <summary>Task faulted with an error.</summary>
    Faulted
}

/// <summary>Manages all engine tasks.</summary>
internal interface ITaskManager
{
    /// <summary>Gets all currently running tasks.</summary>
    IReadOnlyList<EngineTaskBase> RunningTasks { get; }
    /// <summary>Gets all tasks (including completed).</summary>
    IReadOnlyList<EngineTaskBase> AllTasks { get; }

    /// <summary>Gets a task by its ID.</summary>
    EngineTaskBase? GetTask(string taskId);

    /// <summary>Starts a live trading task.</summary>
    Task<string> StartLiveTaskAsync(object config, CancellationToken cancellationToken);
    /// <summary>Stops a live trading task.</summary>
    Task StopLiveTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Starts a backtest task.</summary>
    Task<string> StartBacktestTaskAsync(object input, CancellationToken cancellationToken);
    /// <summary>Cancels a backtest task.</summary>
    Task CancelBacktestTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Starts an optimisation task.</summary>
    Task<string> StartOptimizationTaskAsync(object config, CancellationToken cancellationToken);
    /// <summary>Cancels an optimisation task.</summary>
    Task CancelOptimizationTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Pauses a task.</summary>
    Task PauseTaskAsync(string taskId, CancellationToken cancellationToken);
    /// <summary>Resumes a task.</summary>
    Task ResumeTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Gets the state of a task.</summary>
    Task<object> GetTaskStateAsync(string taskId, CancellationToken cancellationToken);
    /// <summary>Gets the result of an optimisation task.</summary>
    Task<object> GetOptimizationResultAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Stops all tasks.</summary>
    Task StopAllTasksAsync(CancellationToken cancellationToken);
    /// <summary>Stops all user tasks (Live, Backtest, Optimisation).</summary>
    Task StopAllUserTasksAsync(CancellationToken cancellationToken);

    /// <summary>Injects genes into a live task.</summary>
    Task InjectGenesAsync(string taskId, double[] genes, CancellationToken cancellationToken);

    /// <summary>Gets the live state of a live task.</summary>
    Task<object> GetLiveStateAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>Stops all live trading tasks gracefully.</summary>
    Task StopAllLiveTasksAsync(CancellationToken cancellationToken);

    /// <summary>Gets the timestamp of the last tick received by the live task, if any.</summary>
    DateTime? GetLastLiveTickTimestamp();

    /// <summary>Restores a live task from persisted state.</summary>
    /// <param name="state">The persisted live state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task ID if restoration succeeded, otherwise <c>null</c>.</returns>
    Task<string?> RestoreLiveTaskAsync(LiveState state, CancellationToken cancellationToken);
}
