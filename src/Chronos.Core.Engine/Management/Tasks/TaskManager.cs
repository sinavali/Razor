using System.Collections.Concurrent;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Tasks;

internal sealed class TaskManager : ITaskManager, IDisposable
{
    private readonly ConcurrentDictionary<string, EngineTask> _tasks = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TaskManager> _logger;
    private readonly IStateManager _stateManager;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 0, "Task {TaskId} faulted.");

    public TaskManager(ILogger<TaskManager> logger, IStateManager stateManager, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _stateManager = stateManager;
        _loggerFactory = loggerFactory;
    }

    public IReadOnlyList<EngineTask> RunningTasks => _tasks.Values.Where(t => t.State == TaskState.Running).ToList().AsReadOnly();
    public IReadOnlyList<EngineTask> AllTasks => _tasks.Values.ToList().AsReadOnly();
    public EngineTask? GetTask(string taskId) => _tasks.TryGetValue(taskId, out var task) ? task : null;

    public async Task<string> StartLiveTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"live_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var task = new LiveTask(taskId, config, _loggerFactory.CreateLogger<LiveTask>(), this);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);
            await _stateManager.SaveLiveStateAsync(config, cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        finally { _lock.Release(); }
    }

    public async Task StopLiveTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                await task.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                task.State = TaskState.Canceled;
            }
        }
        finally { _lock.Release(); }
    }

    public async Task<string> StartBacktestTaskAsync(object input, CancellationToken cancellationToken)
    {
        var taskId = $"bt_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var task = new BacktestTask(taskId, input, _loggerFactory.CreateLogger<BacktestTask>(), this);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);
            return taskId;
        }
        finally { _lock.Release(); }
    }

    public async Task CancelBacktestTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                await task.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                task.State = TaskState.Canceled;
            }
        }
        finally { _lock.Release(); }
    }

    public async Task<string> StartOptimizationTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"opt_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var task = new OptimizationTask(taskId, config, _loggerFactory.CreateLogger<OptimizationTask>(), this);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);
            await _stateManager.SaveOptimizationStateAsync(taskId, config, cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        finally { _lock.Release(); }
    }

    public async Task CancelOptimizationTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                await task.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                task.State = TaskState.Canceled;
            }
        }
        finally { _lock.Release(); }
    }

    public async Task PauseTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await task.PauseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally { _lock.Release(); }
    }

    public async Task ResumeTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await task.ResumeAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally { _lock.Release(); }
    }

    public async Task<object> GetTaskStateAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            return await task.GetStateAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new EngineException($"Task {taskId} not found.");
    }

    public async Task<object> GetOptimizationResultAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task is OptimizationTask optTask)
        {
            return await optTask.GetResultAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new EngineException($"Optimization task {taskId} not found.");
    }

    public async Task StopAllTasksAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var task in _tasks.Values)
            {
                await task.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                task.State = TaskState.Canceled;
            }
            _tasks.Clear();
        }
        finally { _lock.Release(); }
    }

    public async Task StopAllUserTasksAsync(CancellationToken cancellationToken)
    {
        var userTypes = new[] { "Live", "Backtest", "Optimization" };
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var kv in _tasks.Where(kv => userTypes.Contains(kv.Value.TaskType)).ToList())
            {
                await kv.Value.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                kv.Value.State = TaskState.Canceled;
                _tasks.TryRemove(kv.Key, out _);
            }
        }
        finally { _lock.Release(); }
    }

    public async Task StopAllLiveTasksAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var liveTasks = _tasks.Where(kv => kv.Value.TaskType == "Live").ToList();
            foreach (var kv in liveTasks)
            {
                await kv.Value.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                kv.Value.State = TaskState.Canceled;
                _tasks.TryRemove(kv.Key, out _);
            }
        }
        finally { _lock.Release(); }
    }

    public async Task InjectGenesAsync(string taskId, double[] genes, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryGetValue(taskId, out var task) && task is LiveTask liveTask)
            {
                await liveTask.InjectGenesAsync(genes, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new EngineException($"Live task {taskId} not found.");
            }
        }
        finally { _lock.Release(); }
    }

    public async Task<object> GetLiveStateAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task is LiveTask liveTask)
        {
            return await liveTask.GetLiveStateAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new EngineException($"Live task {taskId} not found.");
    }

    private async Task ExecuteTaskAsync(EngineTask task, CancellationToken cancellationToken)
    {
        try
        {
            await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            task.State = TaskState.Canceled;
        }
        catch (Exception ex)
        {
            task.State = TaskState.Faulted;
            _logTaskFaulted(_logger, task.TaskId, ex);
        }
        finally
        {
            task.EndTime = DateTime.UtcNow;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
                _tasks.TryRemove(task.TaskId, out _);
            }, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var task in _tasks.Values)
        {
            task.CancellationTokenSource.Cancel();
        }

        _lock.Dispose();
    }
}
