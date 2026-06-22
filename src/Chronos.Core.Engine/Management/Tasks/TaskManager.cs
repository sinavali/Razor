using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Core.Exceptions;
using Chronos.Core.Engine.Kernel;
using Chronos.Core.Kernel.Backtesting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using LiveState = Chronos.Core.Engine.Core.LiveState;

namespace Chronos.Core.Engine.Management.Tasks;

/// <summary>Default implementation of <see cref="ITaskManager"/>.</summary>
internal sealed class TaskManager : ITaskManager, IDisposable
{
    private readonly ConcurrentDictionary<string, EngineTaskBase> _tasks = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TaskManager> _logger;
    private readonly IStateManager _stateManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IKernelService _kernelService;
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 0, "Task {TaskId} faulted.");

    private static readonly Action<ILogger, Exception?> _logLiveTaskRestoreNotImplemented =
        LoggerMessage.Define(LogLevel.Warning, 15, "Live task restoration not implemented with IKernelService; ignoring.");

    /// <summary>Initialises a new instance of the <see cref="TaskManager"/> class.</summary>
    public TaskManager(
        ILogger<TaskManager> logger,
        IStateManager stateManager,
        ILoggerFactory loggerFactory,
        IKernelService kernelService)
    {
        _logger = logger;
        _stateManager = stateManager;
        _loggerFactory = loggerFactory;
        _kernelService = kernelService;
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

        _logLiveTaskRestoreNotImplemented(_logger, null);
        return null;
    }

    /// <inheritdoc/>
    public async Task<string> StartLiveTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"live_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Build LiveInput from config (dummy for now)
            var liveInput = new LiveInput
            {
                AdapterName = config?.GetType().GetProperty("AdapterName")?.GetValue(config)?.ToString() ?? "MockAdapter",
                StrategyName = config?.GetType().GetProperty("StrategyName")?.GetValue(config)?.ToString() ?? "MockStrategy",
                StrategyConfig = config ?? new object(),
                MagicNumber = 12345,
                Leverage = 100,
                InitialBalance = 10000,
                Symbols = new[] { "EURUSD" },
                OrderGuardTimeoutSeconds = 5,
                StopOutLevel = 0.5,
                MaxOpenPositions = 5,
                Genes = Array.Empty<double>(),
                NeuralNetworkName = string.Empty
            };

            // Start via kernel service
            string kernelTaskId = await _kernelService.StartLiveAsync(liveInput, cancellationToken).ConfigureAwait(false);

            var task = new LiveTask(taskId, config ?? new object(), _loggerFactory.CreateLogger<LiveTask>(), this, _kernelService, kernelTaskId);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);

            // Persist state (basic info)
            var state = new LiveState
            {
                TaskId = taskId,
                Config = config ?? new object(),
                Genes = Array.Empty<double>(),
                StartTime = task.StartTime,
                LastTickTime = null,
                StrategyName = liveInput.StrategyName,
                AdapterName = liveInput.AdapterName
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

                // If it's a live task, stop the kernel session
                if (task is LiveTask liveTask)
                {
                    await _kernelService.StopLiveAsync(liveTask.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }
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
            // Build BacktestInput from input (dummy)
            var backtestInput = new BacktestInput
            {
                TickStreams = Array.Empty<IReadOnlyList<Tick>>(),
                Symbols = Array.Empty<string>(),
                Strategy = null!,
                StrategySpecification = null!,
                ExecutionSpecification = null!,
                MarketCalculator = null!,
                SymbolProperties = new Dictionary<string, SymbolProperties>()
            };

            // Start via kernel service
            string kernelTaskId = await _kernelService.StartBacktestAsync(backtestInput, cancellationToken).ConfigureAwait(false);

            var task = new BacktestTask(taskId, input ?? new object(), _loggerFactory.CreateLogger<BacktestTask>(), this, _kernelService, kernelTaskId);
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
                // No explicit kernel cancel for backtest (stub)
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
            // Build OptimizationInput (dummy)
            var optInput = new OptimizationInput
            {
                AdapterName = "MockAdapter",
                StrategyName = "MockStrategy",
                StrategyConfig = config ?? new object(),
                Leverage = 100,
                InitialBalance = 10000,
                Symbols = new[] { "EURUSD" },
                MasterSeed = 42,
                Generations = 10,
                PopulationSize = 50,
                MutationRate = 0.1,
                CrossoverRate = 0.5,
                ElitismPct = 0.05,
                TournamentSize = 3,
                StagnationGenerationsBeforeHyper = 3,
                MaxParallelThreads = 0,
                NeuralNetworkName = string.Empty
            };

            string kernelTaskId = await _kernelService.StartOptimizationAsync(optInput, cancellationToken).ConfigureAwait(false);

            var task = new OptimizationTask(taskId, config ?? new object(), _loggerFactory.CreateLogger<OptimizationTask>(), this, _kernelService, kernelTaskId);
            _tasks[taskId] = task;
            _ = Task.Run(() => ExecuteTaskAsync(task, cancellationToken), cancellationToken);

            // Persist initial state
            var state = new OptimizationState
            {
                TaskId = taskId,
                Config = config ?? new object(),
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
                // No explicit kernel cancel for optimisation (stub)
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
                if (task is LiveTask liveTask)
                {
                    await _kernelService.PauseLiveAsync(liveTask.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }
                // For optimisation, we could also pause, but stub doesn't support.
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
                if (task is LiveTask liveTask)
                {
                    await _kernelService.ResumeLiveAsync(liveTask.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }
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
                // Cancel kernel sessions
                if (task is LiveTask lt)
                {
                    await _kernelService.StopLiveAsync(lt.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }
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
                if (kv.Value is LiveTask lt)
                {
                    await _kernelService.StopLiveAsync(lt.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }

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
                if (kv.Value is LiveTask lt)
                {
                    await _kernelService.StopLiveAsync(lt.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }

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
                await _kernelService.InjectGenesAsync(liveTask.KernelTaskId, genes, cancellationToken).ConfigureAwait(false);

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
