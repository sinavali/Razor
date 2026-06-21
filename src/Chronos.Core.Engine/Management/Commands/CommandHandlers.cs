// -----------------------------------------------------------------------------
// <copyright file="CommandHandlers.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands;

using System.Globalization;
using System.Text.Json;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Scheduling;
using Chronos.Core.Engine.Management.Tasks;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Chronos.Core.Engine.Services.Mining;
using Microsoft.Extensions.Logging;

/// <summary>Base class for command handlers.</summary>
internal abstract class CommandHandlerBase : ICommandHandler
{
    protected readonly ICloudConnector CloudConnector;
    protected readonly ICommandDispatcher Dispatcher;
    protected readonly ILogger Logger;

    protected CommandHandlerBase(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger logger)
    {
        CloudConnector = cloudConnector;
        Dispatcher = dispatcher;
        Logger = logger;
    }

    public abstract int CommandId { get; }
    public abstract Task HandleAsync(CloudCommand command, CancellationToken cancellationToken);

    protected async Task SendResponseAsync(string correlationId, object? result, string? error = null, CancellationToken cancellationToken = default)
    {
        await Dispatcher.SendResponseAsync(CommandId, correlationId, result, error, cancellationToken).ConfigureAwait(false);
    }

    protected async Task SendSuccessAsync(string correlationId, object? result, CancellationToken cancellationToken = default)
    {
        await SendResponseAsync(correlationId, result, null, cancellationToken).ConfigureAwait(false);
    }

    protected async Task SendErrorAsync(string correlationId, string error, CancellationToken cancellationToken = default)
    {
        await SendResponseAsync(correlationId, null, error, cancellationToken).ConfigureAwait(false);
    }
}

// ─── System Management ──────────────────────────────────────────

internal sealed class GetStatusHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetStatusHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetStatusHandler> logger, ITaskManager taskManager)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1003;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var status = new
        {
            EngineVersion = AppConstants.EngineVersion,
            IsConnected = CloudConnector.IsConnected,
            SessionId = CloudConnector.SessionId,
            Uptime = TimeSpan.Zero,
            Tasks = new
            {
                Running = _taskManager.RunningTasks.Count,
                Total = _taskManager.AllTasks.Count
            }
        };
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, status, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ShutdownHandler : CommandHandlerBase
{
    private static readonly Action<ILogger, Exception?> _logShutdownWarning =
        LoggerMessage.Define(LogLevel.Warning, 0, "Shutdown command received. Initiating shutdown...");

    public ShutdownHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<ShutdownHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1006;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logShutdownWarning(Logger, null);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Shutdown initiated." }, cancellationToken).ConfigureAwait(false);
        Environment.Exit(0);
    }
}

internal sealed class GetCapabilitiesHandler : CommandHandlerBase
{
    public GetCapabilitiesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetCapabilitiesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1010;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var result = new
        {
            EngineVersion = AppConstants.EngineVersion,
            SdkVersion = AppConstants.SdkVersion,
            Capabilities = AppConstants.SupportedCapabilities
        };
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, result, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Live Trading ────────────────────────────────────────────────

internal sealed class StartLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public StartLiveHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<StartLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1100;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var config = command.Parameters ?? new object();
        string taskId = await _taskManager.StartLiveTaskAsync(config, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class StopLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public StopLiveHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<StopLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1101;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        string? taskId = null;
        if (command.Parameters is Dictionary<string, object> dict && dict.TryGetValue("TaskId", out object? idObj))
        {
            taskId = idObj?.ToString();
        }

        if (string.IsNullOrEmpty(taskId))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await _taskManager.StopLiveTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class InjectGenesHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, int, Exception?> _logInjectingGenes =
        LoggerMessage.Define<int>(LogLevel.Information, 0, "Injecting {Count} genes into live task.");

    public InjectGenesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<InjectGenesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1102;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("TaskId", out object? taskIdObj) ||
            !dict.TryGetValue("Genes", out object? genesObj) ||
            genesObj is not double[] genes)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing or invalid TaskId or Genes parameter.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = taskIdObj?.ToString()!;
        _logInjectingGenes(Logger, genes.Length, null);

        await _taskManager.InjectGenesAsync(taskId, genes, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { GenesCount = genes.Length }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetLiveStateHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetLiveStateHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<GetLiveStateHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1105;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        object state = await _taskManager.GetLiveStateAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, state, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Backtesting ──────────────────────────────────────────────────

internal sealed class RunBacktestHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public RunBacktestHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<RunBacktestHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1200;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object input = command.Parameters ?? new object();
        string taskId = await _taskManager.StartBacktestTaskAsync(input, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class CancelBacktestHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public CancelBacktestHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<CancelBacktestHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1201;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        await _taskManager.CancelBacktestTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetBacktestResultHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetBacktestResultHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<GetBacktestResultHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1202;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        object state = await _taskManager.GetTaskStateAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId, State = state }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ListBacktestsHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public ListBacktestsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<ListBacktestsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1203;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var tasks = _taskManager.AllTasks.Select(t => new
        {
            t.TaskId,
            t.TaskType,
            State = t.State.ToString(),
            t.StartTime,
            t.EndTime
        }).ToArray();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Tasks = tasks }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Optimization ────────────────────────────────────────────────

internal sealed class StartOptimizationHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public StartOptimizationHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<StartOptimizationHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1300;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object config = command.Parameters ?? new object();
        string taskId = await _taskManager.StartOptimizationTaskAsync(config, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class CancelOptimizationHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public CancelOptimizationHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<CancelOptimizationHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1301;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        await _taskManager.CancelOptimizationTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class PauseOptimizationHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public PauseOptimizationHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<PauseOptimizationHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1302;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        await _taskManager.PauseTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ResumeOptimizationHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public ResumeOptimizationHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<ResumeOptimizationHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1303;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        await _taskManager.ResumeTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetOptimizationStateHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetOptimizationStateHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<GetOptimizationStateHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1304;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        object state = await _taskManager.GetTaskStateAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId, State = state }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetOptimizationResultHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetOptimizationResultHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<GetOptimizationResultHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1305;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        object result = await _taskManager.GetOptimizationResultAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, result, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ListOptimizationsHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public ListOptimizationsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<ListOptimizationsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1306;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var tasks = _taskManager.AllTasks.Where(t => t.TaskType == "Optimization").Select(t => new
        {
            t.TaskId,
            State = t.State.ToString(),
            t.StartTime,
            t.EndTime
        }).ToArray();

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Tasks = tasks }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Extensions ──────────────────────────────────────────────────

internal sealed class ReloadExtensionsHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public ReloadExtensionsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<ReloadExtensionsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => 1400;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        await _extensionManager.ReloadExtensionsAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Extensions reloaded." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeployExtensionHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public DeployExtensionHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<DeployExtensionHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => 1401;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("Name", out object? nameObj) ||
            !dict.TryGetValue("BinaryData", out object? dataObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Name or BinaryData.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string name = nameObj?.ToString()!;
        byte[] data = dataObj as byte[] ?? Array.Empty<byte>();

        await _extensionManager.DeployExtensionAsync(name, data, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Name = name }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class RemoveExtensionHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public RemoveExtensionHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<RemoveExtensionHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => 1402;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("Name", out object? nameObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Name.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string name = nameObj?.ToString()!;
        await _extensionManager.RemoveExtensionAsync(name, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Name = name }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ListExtensionsHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public ListExtensionsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<ListExtensionsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => 1403;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object manifest = await _extensionManager.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, manifest, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ActivateExtensionsHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public ActivateExtensionsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<ActivateExtensionsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => 1404;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("Names", out object? namesObj) ||
            namesObj is not object[] names)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing or invalid Names parameter.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string[] nameList = names.Select(n => n.ToString()!).ToArray();
        await _extensionManager.ActivateExtensionsAsync(nameList, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Activated = nameList }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Reports ──────────────────────────────────────────────────────

internal sealed class GenerateReportHandler : CommandHandlerBase
{
    public GenerateReportHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GenerateReportHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1500;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("ReportType", out object? typeObj) ||
            !dict.TryGetValue("Data", out object? dataObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ReportType or Data.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string reportType = typeObj?.ToString() ?? "json";
        string reportId = $"rpt_{Guid.NewGuid():N}";
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ReportId = reportId, Format = reportType }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetReportHandler : CommandHandlerBase
{
    public GetReportHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetReportHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1501;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("ReportId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ReportId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string reportId = idObj?.ToString()!;
        string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports", $"{reportId}.json");

        byte[] data = Array.Empty<byte>();
        if (File.Exists(reportPath))
        {
            data = await File.ReadAllBytesAsync(reportPath, cancellationToken).ConfigureAwait(false);
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ReportId = reportId, Data = Convert.ToBase64String(data) }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Logs ──────────────────────────────────────────────────────────

internal sealed class GetLogsHandler : CommandHandlerBase
{
    private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    public GetLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1600;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("LogType", out object? typeObj) ||
            !dict.TryGetValue("Date", out object? dateObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing LogType or Date.", cancellationToken).ConfigureAwait(false);
            return;
        }

        DateTime date = DateTime.Parse(dateObj?.ToString()!, CultureInfo.InvariantCulture);
        string pattern = $"chronos-{date:yyyy-MM-dd}-*.log";
        string[] files = Directory.GetFiles(_logDirectory, pattern);
        string? logFile = files.FirstOrDefault();

        if (logFile == null)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Log file not found.", cancellationToken).ConfigureAwait(false);
            return;
        }

        byte[] data = await File.ReadAllBytesAsync(logFile, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new
        {
            FileName = Path.GetFileName(logFile),
            Size = data.Length,
            Data = Convert.ToBase64String(data)
        }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeleteLogsAllHandler : CommandHandlerBase
{
    private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static readonly Action<ILogger, string, Exception?> _logDeletedLogFile =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Deleted log file: {File}");

    private static readonly Action<ILogger, string, Exception?> _logDeleteLogError =
        LoggerMessage.Define<string>(LogLevel.Error, 1, "Error deleting log file: {File}");

    public DeleteLogsAllHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<DeleteLogsAllHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1601;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.GetFiles(_logDirectory, "chronos-*.log"))
        {
            try
            {
                File.Delete(file);
                _logDeletedLogFile(Logger, Path.GetFileName(file), null);
            }
            catch (Exception ex)
            {
                _logDeleteLogError(Logger, Path.GetFileName(file), ex);
            }
        }
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "All logs deleted." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeleteLogsExpiredHandler : CommandHandlerBase
{
    private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static readonly Action<ILogger, string, Exception?> _logDeleteExpiredInfo =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Deleted expired log: {File}");

    private static readonly Action<ILogger, string, Exception?> _logDeleteExpiredError =
        LoggerMessage.Define<string>(LogLevel.Error, 1, "Error deleting log: {File}");

    public DeleteLogsExpiredHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<DeleteLogsExpiredHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1602;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-30);
        foreach (string file in Directory.GetFiles(_logDirectory, "chronos-*.log"))
        {
            string fileName = Path.GetFileName(file);
            string datePart = fileName.Substring(8, 10);
            if (DateTime.TryParse(datePart, CultureInfo.InvariantCulture, out DateTime fileDate) && fileDate < cutoff)
            {
                try
                {
                    File.Delete(file);
                    _logDeleteExpiredInfo(Logger, fileName, null);
                }
                catch (Exception ex)
                {
                    _logDeleteExpiredError(Logger, fileName, ex);
                }
            }
        }
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Expired logs deleted." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class SetLogLevelHandler : CommandHandlerBase
{
    private static readonly Action<ILogger, string, Exception?> _logSetLogLevel =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Log level set to {Level}.");

    public SetLogLevelHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<SetLogLevelHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1603;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("Level", out object? levelObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Level.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string level = levelObj?.ToString() ?? "Information";
        _logSetLogLevel(Logger, level, null);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Level = level }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetMetricsHandler : CommandHandlerBase
{
    private readonly IEngineTelemetry _telemetry;

    public GetMetricsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IEngineTelemetry telemetry, ILogger<GetMetricsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _telemetry = telemetry;
    }

    public override int CommandId => 1604;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object metrics = _telemetry.GetMetricsSnapshot();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, metrics, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ExportMetricsHandler : CommandHandlerBase
{
    private readonly IEngineTelemetry _telemetry;

    public ExportMetricsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IEngineTelemetry telemetry, ILogger<ExportMetricsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _telemetry = telemetry;
    }

    public override int CommandId => 1605;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        string metricsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"metrics_export_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        object metricsData = _telemetry.GetMetricsSnapshot();
        string json = JsonSerializer.Serialize(metricsData);

        await File.WriteAllTextAsync(metricsFile, json, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Metrics exported.", File = Path.GetFileName(metricsFile) }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Schedules & Cron ────────────────────────────────────────────

internal sealed class SetCronJobHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public SetCronJobHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<SetCronJobHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => 1700;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("JobId", out object? jobIdObj) ||
            !dict.TryGetValue("CronExpression", out object? cronObj) ||
            !dict.TryGetValue("Command", out object? cmdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing JobId, CronExpression, or Command.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string jobId = jobIdObj?.ToString()!;
        string cron = cronObj?.ToString()!;
        string cmd = cmdObj?.ToString()!;
        bool enabled = dict.TryGetValue("Enabled", out object? enabledObj) && enabledObj is bool e && e;

        await _cronJobManager.SetCronJobAsync(jobId, cron, cmd, enabled, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { JobId = jobId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeleteCronJobHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public DeleteCronJobHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<DeleteCronJobHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => 1701;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("JobId", out object? jobIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing JobId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string jobId = jobIdObj?.ToString()!;
        await _cronJobManager.DeleteCronJobAsync(jobId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { JobId = jobId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ListCronJobsHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public ListCronJobsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<ListCronJobsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => 1702;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object jobs = await _cronJobManager.ListCronJobsAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, jobs, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class SetScheduleHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public SetScheduleHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<SetScheduleHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => 1703;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("ScheduleId", out object? idObj) ||
            !dict.TryGetValue("ScheduledTimeUtc", out object? timeObj) ||
            !dict.TryGetValue("Command", out object? cmdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ScheduleId, ScheduledTimeUtc, or Command.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string scheduleId = idObj?.ToString()!;
        DateTime scheduledTime = DateTime.Parse(timeObj?.ToString()!, CultureInfo.InvariantCulture);
        string cmd = cmdObj?.ToString()!;
        bool repeat = dict.TryGetValue("Repeat", out object? repeatObj) && repeatObj is bool r && r;

        await _cronJobManager.SetScheduleAsync(scheduleId, scheduledTime, cmd, repeat, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ScheduleId = scheduleId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeleteScheduleHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public DeleteScheduleHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<DeleteScheduleHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => 1704;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("ScheduleId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ScheduleId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string scheduleId = idObj?.ToString()!;
        await _cronJobManager.DeleteScheduleAsync(scheduleId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ScheduleId = scheduleId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ListSchedulesHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public ListSchedulesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<ListSchedulesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => 1705;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object schedules = await _cronJobManager.ListSchedulesAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, schedules, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Mining ──────────────────────────────────────────────────────

internal sealed class StartMiningHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public StartMiningHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<StartMiningHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1800;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object config = command.Parameters ?? new object();
        await _miningIntegration.StartAsync(config, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Mining started." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class StopMiningHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public StopMiningHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<StopMiningHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1801;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        await _miningIntegration.StopAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Mining stopped." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetMiningStatusHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public GetMiningStatusHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<GetMiningStatusHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1802;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object status = await _miningIntegration.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, status, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class UpdateMiningConfigHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public UpdateMiningConfigHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<UpdateMiningConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1803;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object config = command.Parameters ?? new object();
        await _miningIntegration.UpdateConfigAsync(config, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Mining config updated." }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Admin & Broadcast ─────────────────────────────────────────

internal sealed class BroadcastMessageHandler : CommandHandlerBase
{
    private static readonly Action<ILogger, Exception?> _logBroadcastMessageWarning =
        LoggerMessage.Define(LogLevel.Warning, 0, "Broadcast message displayed.");

    public BroadcastMessageHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<BroadcastMessageHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1900;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("Text", out object? textObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Text.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string text = textObj?.ToString() ?? string.Empty;
        string style = dict.TryGetValue("Style", out object? styleObj) ? styleObj?.ToString() ?? "info" : "info";

        Console.ForegroundColor = style switch
        {
            "error" => ConsoleColor.Red,
            "warning" => ConsoleColor.Yellow,
            "success" => ConsoleColor.Green,
            _ => ConsoleColor.Cyan
        };
        Console.WriteLine($"\n=== BROADCAST ===\n{text}\n================\n");
        Console.ResetColor();

        _logBroadcastMessageWarning(Logger, null);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Broadcast displayed." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class SetAdminConfigHandler : CommandHandlerBase
{
    private readonly IStateManager _stateManager;

    public SetAdminConfigHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IStateManager stateManager, ILogger<SetAdminConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _stateManager = stateManager;
    }

    public override int CommandId => 1901;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        string configJson = JsonSerializer.Serialize(command.Parameters);
        await _stateManager.SetMetadataAsync("AdminConfig", configJson, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Admin config updated." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetEngineCapabilitiesHandler : CommandHandlerBase
{
    public GetEngineCapabilitiesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetEngineCapabilitiesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1902;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var result = new
        {
            EngineVersion = AppConstants.EngineVersion,
            Capabilities = AppConstants.SupportedCapabilities
        };
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, result, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetEngineVersionHandler : CommandHandlerBase
{
    public GetEngineVersionHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetEngineVersionHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1903;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Version = AppConstants.EngineVersion }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Kill & Emergency ──────────────────────────────────────────

internal sealed class KillSwitchHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, Exception?> _logKillSwitchActivated =
        LoggerMessage.Define(LogLevel.Warning, 0, "KILL SWITCH ACTIVATED!");

    public KillSwitchHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<KillSwitchHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 2000;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logKillSwitchActivated(Logger, null);
        await _taskManager.StopAllTasksAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Kill switch activated. All tasks stopped." }, cancellationToken).ConfigureAwait(false);

        Environment.Exit(1);
    }
}

internal sealed class EmergencyStopHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, Exception?> _logEmergencyStop =
        LoggerMessage.Define(LogLevel.Warning, 0, "EMERGENCY STOP!");

    public EmergencyStopHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<EmergencyStopHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 2001;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logEmergencyStop(Logger, null);
        await _taskManager.StopAllTasksAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Emergency stop executed." }, cancellationToken).ConfigureAwait(false);
    }
}

// ─── Behavior Logging ──────────────────────────────────────────

internal sealed class EnableBehaviorLoggingHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public EnableBehaviorLoggingHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<EnableBehaviorLoggingHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2100;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj) ||
            !dict.TryGetValue("StrategyName", out object? strategyNameObj) ||
            !dict.TryGetValue("Genes", out object? genesObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId, StrategyName, or Genes.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        string strategyName = strategyNameObj?.ToString()!;
        double[] genes = (genesObj as double[]) ?? Array.Empty<double>();

        _behaviorRecorder.Enable(sessionId, strategyName, genes);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { SessionId = sessionId }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DisableBehaviorLoggingHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public DisableBehaviorLoggingHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<DisableBehaviorLoggingHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2101;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _behaviorRecorder.Disable();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Behavior logging disabled." }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GetBehaviorLogsHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public GetBehaviorLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<GetBehaviorLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2102;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        object logs = await _behaviorRecorder.GetLogsAsync(sessionId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, logs, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeleteBehaviorLogsHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public DeleteBehaviorLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<DeleteBehaviorLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2103;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        await _behaviorRecorder.DeleteLogsAsync(sessionId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { SessionId = sessionId }, cancellationToken).ConfigureAwait(false);
    }
}
