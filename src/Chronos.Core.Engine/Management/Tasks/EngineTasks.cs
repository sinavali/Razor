using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Tasks;

/// <summary>Live trading task.</summary>
internal sealed class LiveTask : EngineTaskBase
{
    private readonly ILogger<LiveTask> _logger;
    private readonly ITaskManager _taskManager;
    private double[] _genes = Array.Empty<double>();

    /// <summary>Gets the configuration object used to start this task.</summary>
    public object Config { get; }

    /// <summary>Gets or sets the name of the active strategy.</summary>
    public string StrategyName { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the active adapter.</summary>
    public string AdapterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp of the last tick received (UTC).</summary>
    public DateTime? LastTickTime { get; set; }

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, Exception?> _logLiveTaskStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Live task {TaskId} started.");
    private static readonly Action<ILogger, string, Exception?> _logLiveTaskCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Live task {TaskId} completed.");
    private static readonly Action<ILogger, string, Exception?> _logLiveTaskCanceled =
        LoggerMessage.Define<string>(LogLevel.Information, 2, "Live task {TaskId} canceled.");
    private static readonly Action<ILogger, string, Exception?> _logLiveTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 3, "Live task {TaskId} faulted.");
    private static readonly Action<ILogger, int, string, Exception?> _logInjectedGenes =
        LoggerMessage.Define<int, string>(LogLevel.Information, 4, "Injected {Count} genes into live task {TaskId}.");

    public LiveTask(string taskId, object config, ILogger<LiveTask> logger, ITaskManager taskManager)
    {
        TaskId = taskId;
        TaskType = "Live";
        Config = config;
        _logger = logger;
        _taskManager = taskManager;
        StartTime = DateTime.UtcNow;
        State = TaskState.Initializing;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Running;
        _logLiveTaskStarted(_logger, TaskId, null);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Simulate tick processing – in real implementation, this would be driven by adapter ticks.
                // For now, just update tick time with current UTC time periodically.
                LastTickTime = DateTime.UtcNow;
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            State = TaskState.Completed;
            _logLiveTaskCompleted(_logger, TaskId, null);
        }
        catch (OperationCanceledException)
        {
            State = TaskState.Canceled;
            _logLiveTaskCanceled(_logger, TaskId, null);
        }
        catch (Exception ex)
        {
            State = TaskState.Faulted;
            _logLiveTaskFaulted(_logger, TaskId, ex);
            throw;
        }
        finally
        {
            EndTime = DateTime.UtcNow;
        }
    }

    /// <summary>Injects a gene array into the live strategy.</summary>
    public async Task InjectGenesAsync(double[] genes, CancellationToken cancellationToken)
    {
        _genes = genes ?? Array.Empty<double>();
        _logInjectedGenes(_logger, _genes.Length, TaskId, null);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Returns the currently active gene array.</summary>
    public Task<double[]> GetGenesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_genes);
    }

    /// <summary>Gets a snapshot of the live trading state.</summary>
    public Task<object> GetLiveStateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new
        {
            IsLive = State == TaskState.Running,
            Equity = 10000.0,
            Balance = 10000.0,
            Drawdown = 0.0,
            Positions = Array.Empty<object>(),
            Orders = Array.Empty<object>()
        });
    }
}

/// <summary>Backtest task.</summary>
internal sealed class BacktestTask : EngineTaskBase
{
    private readonly object _input;
    private readonly ILogger<BacktestTask> _logger;
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, string, Exception?> _logBacktestTaskStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Backtest task {TaskId} started.");
    private static readonly Action<ILogger, string, Exception?> _logBacktestTaskCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Backtest task {TaskId} completed.");
    private static readonly Action<ILogger, string, Exception?> _logBacktestTaskCanceled =
        LoggerMessage.Define<string>(LogLevel.Information, 2, "Backtest task {TaskId} canceled.");
    private static readonly Action<ILogger, string, Exception?> _logBacktestTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 3, "Backtest task {TaskId} faulted.");

    public BacktestTask(string taskId, object input, ILogger<BacktestTask> logger, ITaskManager taskManager)
    {
        TaskId = taskId;
        TaskType = "Backtest";
        _input = input;
        _logger = logger;
        _taskManager = taskManager;
        StartTime = DateTime.UtcNow;
        State = TaskState.Initializing;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Running;
        _logBacktestTaskStarted(_logger, TaskId, null);
        try
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            State = TaskState.Completed;
            _logBacktestTaskCompleted(_logger, TaskId, null);
        }
        catch (OperationCanceledException)
        {
            State = TaskState.Canceled;
            _logBacktestTaskCanceled(_logger, TaskId, null);
        }
        catch (Exception ex)
        {
            State = TaskState.Faulted;
            _logBacktestTaskFaulted(_logger, TaskId, ex);
            throw;
        }
        finally
        {
            EndTime = DateTime.UtcNow;
        }
    }
}

/// <summary>Optimization task.</summary>
internal sealed class OptimizationTask : EngineTaskBase
{
    private readonly object _config;
    private readonly ILogger<OptimizationTask> _logger;
    private readonly ITaskManager _taskManager;
    private object? _result;

    /// <summary>Gets the configuration object used to start this task.</summary>
    public object Config => _config;

    private static readonly Action<ILogger, string, Exception?> _logOptimizationTaskStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Optimization task {TaskId} started.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationTaskCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Optimization task {TaskId} completed.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationTaskCanceled =
        LoggerMessage.Define<string>(LogLevel.Information, 2, "Optimization task {TaskId} canceled.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationTaskFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, 3, "Optimization task {TaskId} faulted.");

    public OptimizationTask(string taskId, object config, ILogger<OptimizationTask> logger, ITaskManager taskManager)
    {
        TaskId = taskId;
        TaskType = "Optimization";
        _config = config;
        _logger = logger;
        _taskManager = taskManager;
        StartTime = DateTime.UtcNow;
        State = TaskState.Initializing;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        State = TaskState.Running;
        _logOptimizationTaskStarted(_logger, TaskId, null);
        try
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            _result = new { BestFitness = 0.0, Generations = 0 };
            State = TaskState.Completed;
            _logOptimizationTaskCompleted(_logger, TaskId, null);
        }
        catch (OperationCanceledException)
        {
            State = TaskState.Canceled;
            _logOptimizationTaskCanceled(_logger, TaskId, null);
        }
        catch (Exception ex)
        {
            State = TaskState.Faulted;
            _logOptimizationTaskFaulted(_logger, TaskId, ex);
            throw;
        }
        finally
        {
            EndTime = DateTime.UtcNow;
        }
    }

    /// <summary>Gets the result of the optimization, if available.</summary>
    public Task<object> GetResultAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_result ?? new { BestFitness = 0.0, Generations = 0 });
    }
}
