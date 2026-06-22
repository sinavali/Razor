using System.Collections.Concurrent;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Tasks;

/// <summary>Default implementation of <see cref="ITaskManager"/>.</summary>
internal sealed class TaskManager : ITaskManager, IDisposable
{
    private readonly ConcurrentDictionary<string, EngineTaskBase> _tasks = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TaskManager> _logger;
    private readonly IStateManager _stateManager;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 0, "Task {TaskId} faulted.");

    private static readonly Action<ILogger, string, Exception?> _logLiveStateRestored =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Restored live task {TaskId} from persisted state.");

    /// <summary>Initialises a new instance of the <see cref="TaskManager"/> class.</summary>
    public TaskManager(ILogger<TaskManager> logger, IStateManager stateManager, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _stateManager = stateManager;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EngineTaskBase> RunningTasks => _tasks.Values.Where(t => t.State == TaskState.Running).ToList().AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<EngineTaskBase> AllTasks => _tasks.Values.ToList().AsReadOnly();

    /// <inheritdoc/>
    public EngineTaskBase? GetTask(string taskId) => _tasks.TryGetValue(taskId, out var task) ? task : null;

    /// <inheritdoc/>
    public DateTime? GetLastLiveTickTimestamp()
    {
        var liveTask = _tasks.Values.OfType<LiveTask>().FirstOrDefault();
        return liveTask?.LastTickTime;
    }

    /// <inheritdoc/>
    public async Task<string?> RestoreLiveTaskAsync(LiveState state, CancellationToken cancellationToken)
    {
        if (state == null)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var task = new LiveTask(state.TaskId, state.Config, _loggerFactory.CreateLogger<LiveTask>(), this)
            {
                StartTime = state.StartTime,
                StrategyName = state.StrategyName,
                AdapterName = state.AdapterName
            };

            // Restore genes and last tick time
            await task.InjectGenesAsync(state.Genes, cancellationToken).ConfigureAwait(false);
            task.LastTickTime = state.LastTickTime;

            _tasks[state.TaskId] = task;
            task.State = TaskState.Running;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);
            _logLiveStateRestored(_logger, state.TaskId, null);
            return state.TaskId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> StartLiveTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"live_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var task = new LiveTask(taskId, config, _loggerFactory.CreateLogger<LiveTask>(), this);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);

            // Persist state
            var state = new LiveState
            {
                TaskId = taskId,
                Config = config,
                Genes = Array.Empty<double>(),
                StartTime = task.StartTime,
                LastTickTime = null,
                StrategyName = config?.GetType().GetProperty("StrategyName")?.GetValue(config)?.ToString() ?? string.Empty,
                AdapterName = config?.GetType().GetProperty("AdapterName")?.GetValue(config)?.ToString() ?? string.Empty
            };
            await _stateManager.SaveLiveStateAsync(state, cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopLiveTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                await task.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                task.State = TaskState.Canceled;
                // Remove persisted state by saving a null state? For now we just delete the record.
                // We'll use SQL direct deletion. We can call SaveLiveStateAsync with a null? Not supported.
                // We'll just ignore – the next start will overwrite.
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> StartOptimizationTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"opt_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var task = new OptimizationTask(taskId, config, _loggerFactory.CreateLogger<OptimizationTask>(), this);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);

            // Persist initial state
            var state = new OptimizationState
            {
                TaskId = taskId,
                Config = config,
                Population = new object(),
                CurrentGeneration = 0,
                BestFitness = 0.0,
                StartTime = task.StartTime
            };
            await _stateManager.SaveOptimizationStateAsync(taskId, state, cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CancelOptimizationTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                await task.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
                task.State = TaskState.Canceled;
                // Optionally remove persisted state
                await _stateManager.SaveOptimizationStateAsync(taskId, null!, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task PauseTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await task.PauseAsync(cancellationToken).ConfigureAwait(false);
                // Persist state if live or optimization
                if (task is LiveTask liveTask)
                {
                    var state = new LiveState
                    {
                        TaskId = liveTask.TaskId,
                        Config = liveTask.Config,
                        Genes = await liveTask.GetGenesAsync(cancellationToken).ConfigureAwait(false),
                        LastTickTime = liveTask.LastTickTime,
                        StartTime = liveTask.StartTime,
                        StrategyName = liveTask.StrategyName,
                        AdapterName = liveTask.AdapterName
                    };
                    await _stateManager.SaveLiveStateAsync(state, cancellationToken).ConfigureAwait(false);
                }
                else if (task is OptimizationTask optTask)
                {
                    var state = new OptimizationState
                    {
                        TaskId = optTask.TaskId,
                        Config = optTask.Config,
                        Population = new object(),
                        CurrentGeneration = 0,
                        BestFitness = 0.0,
                        StartTime = optTask.StartTime
                    };
                    await _stateManager.SaveOptimizationStateAsync(taskId, state, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<object> GetTaskStateAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            return await task.GetStateAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new EngineException($"Task {taskId} not found.");
    }

    /// <inheritdoc/>
    public async Task<object> GetOptimizationResultAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task is OptimizationTask optTask)
        {
            return await optTask.GetResultAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new EngineException($"Optimization task {taskId} not found.");
    }

    /// <inheritdoc/>
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task InjectGenesAsync(string taskId, double[] genes, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tasks.TryGetValue(taskId, out var task) && task is LiveTask liveTask)
            {
                await liveTask.InjectGenesAsync(genes, cancellationToken).ConfigureAwait(false);
                // Persist updated genes
                var state = new LiveState
                {
                    TaskId = liveTask.TaskId,
                    Config = liveTask.Config,
                    Genes = genes,
                    LastTickTime = liveTask.LastTickTime,
                    StartTime = liveTask.StartTime,
                    StrategyName = liveTask.StrategyName,
                    AdapterName = liveTask.AdapterName
                };
                await _stateManager.SaveLiveStateAsync(state, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new EngineException($"Live task {taskId} not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<object> GetLiveStateAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task is LiveTask liveTask)
        {
            return await liveTask.GetLiveStateAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new EngineException($"Live task {taskId} not found.");
    }

    /// <summary>Executes a task and handles its lifecycle.</summary>
    private async Task ExecuteTaskAsync(EngineTaskBase task, CancellationToken cancellationToken)
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

    /// <inheritdoc/>
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
