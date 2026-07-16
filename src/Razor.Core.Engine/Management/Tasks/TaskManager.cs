// -----------------------------------------------------------------------------
// <copyright file="TaskManager.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Tasks;

using Razor.Core.Engine.Core;
using Razor.Core.Engine.Core.Exceptions;
using Razor.Core.Engine.Extensions;
using Razor.Core.Engine.Kernel;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using LiveState = Razor.Core.Engine.Core.LiveState;

/// <summary>Default implementation of <see cref="ITaskManager"/>.</summary>
internal sealed class TaskManager : ITaskManager, IDisposable
{
    private readonly ConcurrentDictionary<string, EngineTaskBase> _tasks = new();
    private readonly ConcurrentDictionary<string, Thread> _liveThreads = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TaskManager> _logger;
    private readonly IStateManager _stateManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IKernelService _kernelService;
    private readonly IExtensionManager _extensionManager;
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 0, "Task {TaskId} faulted.");

    private static readonly Action<ILogger, string, Exception?> _logLiveThreadDidNotExit =
        LoggerMessage.Define<string>(LogLevel.Warning, 16, "Live thread {TaskId} did not exit within 5 seconds.");

    private static readonly Action<ILogger, string, Exception?> _logRestoringLiveTask =
        LoggerMessage.Define<string>(LogLevel.Information, 17, "Restoring live task from persisted state: {TaskId}");

    private static readonly Action<ILogger, string, Exception?> _logLiveTaskRestored =
        LoggerMessage.Define<string>(LogLevel.Information, 18, "Live task {TaskId} restored successfully.");

    private static readonly Action<ILogger, Exception?> _logLiveTaskAlreadyRunning =
        LoggerMessage.Define(LogLevel.Warning, 19, "A live task is already running; skipping restoration.");

    private static readonly Action<ILogger, string, Exception?> _logLiveTaskRestoreFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 20, "Failed to restore live task {TaskId}.");

    /// <summary>Initialises a new instance of the <see cref="TaskManager"/> class.</summary>
    public TaskManager(
        ILogger<TaskManager> logger,
        IStateManager stateManager,
        ILoggerFactory loggerFactory,
        IKernelService kernelService,
        IExtensionManager extensionManager)
    {
        _logger = logger;
        _stateManager = stateManager;
        _loggerFactory = loggerFactory;
        _kernelService = kernelService;
        _extensionManager = extensionManager;
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

        // Check if there is already a live task running (should not happen during startup)
        if (_tasks.Values.Any(t => t.TaskType == "Live" && t.State != TaskState.Completed && t.State != TaskState.Canceled))
        {
            _logLiveTaskAlreadyRunning(_logger, null);
            return null;
        }

        _logRestoringLiveTask(_logger, state.TaskId, null);

        try
        {
            // Build LiveInput from persisted state
            var liveInput = new LiveInput
            {
                AdapterName = state.AdapterName,
                StrategyName = state.StrategyName,
                StrategyConfig = state.Config, // use the stored config object
                MagicNumber = state.MagicNumber,
                Leverage = state.Leverage,
                InitialBalance = state.InitialBalance,
                Symbols = state.Symbols,
                OrderGuardTimeoutSeconds = state.OrderGuardTimeoutSeconds,
                StopOutLevel = state.StopOutLevel,
                MaxOpenPositions = state.MaxOpenPositions,
                Genes = state.Genes,
                NeuralNetworkName = state.NeuralNetworkName ?? string.Empty,
                AccountCurrency = "USD" // TODO: persist and retrieve AccountCurrency from LiveState
            };

            // Start a new live session via kernel service
            string kernelTaskId = await _kernelService.StartLiveAsync(liveInput, cancellationToken).ConfigureAwait(false);

            // Create a new LiveTask with the restored configuration
            var task = new LiveTask(state.TaskId, state.Config, _loggerFactory.CreateLogger<LiveTask>(), this, _kernelService, kernelTaskId)
            {
                StrategyName = state.StrategyName,
                AdapterName = state.AdapterName,
                LastTickTime = state.LastTickTime,
                StartTime = state.StartTime
            };

            _tasks[state.TaskId] = task;

            // Start the live thread
            var thread = new Thread(() =>
            {
                try
                {
                    ExecuteTaskAsync(task, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logTaskFaulted(_logger, task.TaskId, ex);
                }
            })
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true,
                Name = $"LiveThread-{state.TaskId}"
            };

            _liveThreads[state.TaskId] = thread;
            thread.Start();

            _logLiveTaskRestored(_logger, state.TaskId, null);
            return state.TaskId;
        }
        catch (Exception ex)
        {
            _logLiveTaskRestoreFailed(_logger, state.TaskId, ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> StartLiveTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"live_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Parse configuration
            var liveConfig = LiveConfiguration.Parse(config);

            // Build LiveInput
            var liveInput = new LiveInput
            {
                AdapterName = liveConfig.AdapterName,
                StrategyName = liveConfig.StrategyName,
                StrategyConfig = config,
                MagicNumber = liveConfig.MagicNumber,
                Leverage = liveConfig.Leverage,
                InitialBalance = liveConfig.InitialBalance,
                Symbols = liveConfig.Symbols,
                OrderGuardTimeoutSeconds = liveConfig.OrderGuardTimeoutSeconds,
                StopOutLevel = liveConfig.StopOutLevel,
                MaxOpenPositions = liveConfig.MaxOpenPositions,
                Genes = liveConfig.Genes,
                NeuralNetworkName = liveConfig.NeuralNetworkName ?? string.Empty,
                AccountCurrency = liveConfig.AccountCurrency
            };

            // Start via kernel service
            string kernelTaskId = await _kernelService.StartLiveAsync(liveInput, cancellationToken).ConfigureAwait(false);

            var task = new LiveTask(taskId, config ?? new object(), _loggerFactory.CreateLogger<LiveTask>(), this, _kernelService, kernelTaskId)
            {
                StrategyName = liveConfig.StrategyName,
                AdapterName = liveConfig.AdapterName
            };
            _tasks[taskId] = task;

            // Create a dedicated thread for live trading with high priority
            var thread = new Thread(() =>
            {
                try
                {
                    ExecuteTaskAsync(task, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logTaskFaulted(_logger, task.TaskId, ex);
                }
            })
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true,
                Name = $"LiveThread-{taskId}"
            };

            _liveThreads[taskId] = thread;
            thread.Start();

            // Persist complete state
            var state = new LiveState
            {
                TaskId = taskId,
                Config = config ?? new object(),
                AdapterName = liveConfig.AdapterName,
                StrategyName = liveConfig.StrategyName,
                MagicNumber = liveConfig.MagicNumber,
                Leverage = liveConfig.Leverage,
                InitialBalance = liveConfig.InitialBalance,
                Symbols = liveConfig.Symbols,
                OrderGuardTimeoutSeconds = liveConfig.OrderGuardTimeoutSeconds,
                StopOutLevel = liveConfig.StopOutLevel,
                MaxOpenPositions = liveConfig.MaxOpenPositions,
                NeuralNetworkName = liveConfig.NeuralNetworkName ?? string.Empty,
                Genes = liveConfig.Genes,
                LastTickTime = null,
                StartTime = task.StartTime
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
                task.Dispose(); // THR‑05: Dispose CTS.

                // Wait for the live thread to finish
                if (_liveThreads.TryRemove(taskId, out var thread))
                {
                    if (thread.IsAlive)
                    {
                        if (!thread.Join(TimeSpan.FromSeconds(5)))
                        {
                            _logLiveThreadDidNotExit(_logger, taskId, null);
                        }
                    }
                }

                // If it's a live task, stop the kernel session
                if (task is LiveTask liveTask)
                {
                    await _kernelService.StopLiveAsync(liveTask.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }

                // Delete persisted state
                await _stateManager.DeleteLiveStateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> StartBacktestTaskAsync(object config, CancellationToken cancellationToken)
    {
        var taskId = $"bt_{Guid.NewGuid():N}";
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Parse configuration
            var btConfig = BacktestConfiguration.Parse(config);

            // Get active adapter and strategy
            var adapter = _extensionManager.ActiveAdapter
                ?? throw new InvalidOperationException("No active adapter found.");
            var strategy = _extensionManager.ActiveStrategy
                ?? throw new InvalidOperationException("No active strategy found.");

            // Start via kernel service
            string kernelTaskId = await _kernelService.StartBacktestAsync(
                adapter,
                strategy,
                btConfig,
                cancellationToken).ConfigureAwait(false);

            var task = new BacktestTask(taskId, config ?? new object(), _loggerFactory.CreateLogger<BacktestTask>(), this, _kernelService, kernelTaskId);
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
                task.Dispose(); // THR‑05: Dispose CTS.
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
            // Parse configuration into OptimizationInput
            if (config is not Dictionary<string, object> dict)
            {
                throw new ArgumentException("Configuration must be a dictionary.", nameof(config));
            }

            var optInput = new OptimizationInput
            {
                AdapterName = GetString(dict, "AdapterName"),
                StrategyName = GetString(dict, "StrategyName"),
                StrategyConfig = config,
                Leverage = GetDouble(dict, "Leverage", 100),
                InitialBalance = GetDouble(dict, "InitialBalance", 10000),
                Symbols = GetStringArray(dict, "Symbols", ["EURUSD"]),
                MasterSeed = GetInt(dict, "MasterSeed", 42),
                Generations = GetInt(dict, "Generations", 10),
                PopulationSize = GetInt(dict, "PopulationSize", 50),
                MutationRate = GetDouble(dict, "MutationRate", 0.1),
                CrossoverRate = GetDouble(dict, "CrossoverRate", 0.5),
                ElitismPct = GetDouble(dict, "ElitismPct", 0.05),
                TournamentSize = GetInt(dict, "TournamentSize", 3),
                StagnationGenerationsBeforeHyper = GetInt(dict, "StagnationGenerationsBeforeHyper", 3),
                MaxParallelThreads = GetInt(dict, "MaxParallelThreads", 0),
                NeuralNetworkName = GetString(dict, "NeuralNetworkName", string.Empty),
                StartDate = GetDateTime(dict, "StartDate", DateTime.UtcNow.AddDays(-30)),
                EndDate = GetDateTime(dict, "EndDate", DateTime.UtcNow),
                Timeframes = GetStringArray(dict, "Timeframes", ["M1"]),
                AccountCurrency = GetString(dict, "AccountCurrency", "USD")
            };

            // Get active adapter and strategy
            var adapter = _extensionManager.ActiveAdapter
                ?? throw new InvalidOperationException("No active adapter found.");
            var strategy = _extensionManager.ActiveStrategy
                ?? throw new InvalidOperationException("No active strategy found.");

            // Start via kernel service
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

    // Helper methods for parsing config dictionary
    private static string GetString(Dictionary<string, object> dict, string key, string fallback = "")
    {
        if (dict.TryGetValue(key, out object? value) && value is string s)
        {
            return s;
        }
        return fallback;
    }

    private static double GetDouble(Dictionary<string, object> dict, string key, double fallback = 0)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is double d)
            {
                return d;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is long l)
            {
                return l;
            }

            if (value is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }

    private static int GetInt(Dictionary<string, object> dict, string key, int fallback = 0)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is int i)
            {
                return i;
            }

            if (value is long l)
            {
                return (int)l;
            }

            if (value is string s && int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }

    private static string[] GetStringArray(Dictionary<string, object> dict, string key, string[] fallback)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is object[] arr)
            {
                return arr.Select(x => x.ToString()!).ToArray();
            }

            if (value is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray().Select(x => x.GetString()!).ToArray();
            }
        }
        return fallback;
    }

    private static DateTime GetDateTime(Dictionary<string, object> dict, string key, DateTime fallback)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is DateTime dt)
            {
                return dt;
            }

            if (value is string s && DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
        }
        return fallback;
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
                task.Dispose(); // THR‑05: Dispose CTS.
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
                task.Dispose(); // THR‑05: Dispose CTS.
                // Cancel kernel sessions
                if (task is LiveTask lt)
                {
                    await _kernelService.StopLiveAsync(lt.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }
            }

            // Wait for live threads to finish
            foreach (var kv in _liveThreads)
            {
                if (kv.Value.IsAlive)
                {
                    kv.Value.Join(TimeSpan.FromSeconds(5));
                }
            }
            _liveThreads.Clear();
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
                kv.Value.Dispose(); // THR‑05: Dispose CTS.
                if (kv.Value is LiveTask lt)
                {
                    await _kernelService.StopLiveAsync(lt.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }

                _tasks.TryRemove(kv.Key, out _);
                if (_liveThreads.TryRemove(kv.Key, out var thread))
                {
                    if (thread.IsAlive)
                    {
                        thread.Join(TimeSpan.FromSeconds(5));
                    }
                }
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
                kv.Value.Dispose(); // THR‑05: Dispose CTS.
                if (kv.Value is LiveTask lt)
                {
                    await _kernelService.StopLiveAsync(lt.KernelTaskId, cancellationToken).ConfigureAwait(false);
                }

                _tasks.TryRemove(kv.Key, out _);
                if (_liveThreads.TryRemove(kv.Key, out var thread))
                {
                    if (thread.IsAlive)
                    {
                        thread.Join(TimeSpan.FromSeconds(5));
                    }
                }
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

                // Persist updated genes – load existing state, update genes, and save
                var currentState = await _stateManager.LoadLiveStateAsync(cancellationToken).ConfigureAwait(false);
                if (currentState != null)
                {
                    var updatedState = currentState with
                    {
                        Genes = genes,
                        LastTickTime = liveTask.LastTickTime
                    };
                    await _stateManager.SaveLiveStateAsync(updatedState, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Fallback: create a minimal state
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
                if (_tasks.TryRemove(task.TaskId, out var removedTask))
                {
                    removedTask.Dispose(); // Ensure cleanup of CTS even if removed later.
                }
                _liveThreads.TryRemove(task.TaskId, out _);
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
            task.Dispose();
        }

        foreach (var kv in _liveThreads)
        {
            if (kv.Value.IsAlive)
            {
                kv.Value.Join(TimeSpan.FromSeconds(5));
            }
        }

        _lock.Dispose();
    }
}
