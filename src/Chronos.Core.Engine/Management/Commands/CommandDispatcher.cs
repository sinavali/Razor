// -----------------------------------------------------------------------------
// <copyright file="CommandDispatcher.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands;

using System.Collections.Concurrent;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Commands.Handlers;
using Chronos.Core.Engine.Management.Scheduling;
using Chronos.Core.Engine.Management.Tasks;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Chronos.Core.Engine.Services.Mining;
using Microsoft.Extensions.Logging;

/// <summary>Command dispatcher that routes cloud commands to registered handlers.</summary>
internal sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly ConcurrentDictionary<int, ICommandHandler> _handlers = new();
    private readonly ILogger<CommandDispatcher> _logger;
    private readonly ICloudConnector _cloudConnector;
    private readonly ITaskManager _taskManager;
    private readonly IExtensionManager _extensionManager;
    private readonly IEngineTelemetry _telemetry;
    private readonly ICronJobManager _cronJobManager;
    private readonly IMiningIntegration _miningIntegration;
    private readonly IStateManager _stateManager;
    private readonly IBehaviorRecorder _behaviorRecorder;
    private readonly ILoggerFactory _loggerFactory;

    // LoggerMessage delegates
    private static readonly Action<ILogger, int, Exception?> _logRegisterHandler =
        LoggerMessage.Define<int>(LogLevel.Debug, 0, "Registered handler for command {CommandId}.");

    private static readonly Action<ILogger, int, Exception?> _logNoHandler =
        LoggerMessage.Define<int>(LogLevel.Warning, 1, "No handler for command {CommandId}.");

    private static readonly Action<ILogger, int, Exception?> _logCommandError =
        LoggerMessage.Define<int>(LogLevel.Error, 2, "Error executing command {CommandId}.");

    private static readonly Action<ILogger, Exception?> _logCannotSendResponse =
        LoggerMessage.Define(LogLevel.Warning, 3, "Cannot send response, not connected.");

    private static readonly Action<ILogger, Exception?> _logSendResponseError =
        LoggerMessage.Define(LogLevel.Error, 4, "Error sending command response.");

    /// <summary>Initialises a new instance of the <see cref="CommandDispatcher"/> class.</summary>
    /// <param name="cloudConnector">Cloud connector.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="extensionManager">Extension manager.</param>
    /// <param name="telemetry">Telemetry service.</param>
    /// <param name="cronJobManager">Cron job manager.</param>
    /// <param name="miningIntegration">Mining integration.</param>
    /// <param name="stateManager">State manager.</param>
    /// <param name="behaviorRecorder">Behavior recorder.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public CommandDispatcher(
        ICloudConnector cloudConnector,
        ILogger<CommandDispatcher> logger,
        ITaskManager taskManager,
        IExtensionManager extensionManager,
        IEngineTelemetry telemetry,
        ICronJobManager cronJobManager,
        IMiningIntegration miningIntegration,
        IStateManager stateManager,
        IBehaviorRecorder behaviorRecorder,
        ILoggerFactory loggerFactory)
    {
        _cloudConnector = cloudConnector;
        _logger = logger;
        _taskManager = taskManager;
        _extensionManager = extensionManager;
        _telemetry = telemetry;
        _cronJobManager = cronJobManager;
        _miningIntegration = miningIntegration;
        _stateManager = stateManager;
        _behaviorRecorder = behaviorRecorder;
        _loggerFactory = loggerFactory;
        this.RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        this.RegisterHandler(1003, new GetStatusHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetStatusHandler>(), _taskManager));
        this.RegisterHandler(1006, new ShutdownHandler(_cloudConnector, this, _loggerFactory.CreateLogger<ShutdownHandler>()));
        this.RegisterHandler(1010, new GetCapabilitiesHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetCapabilitiesHandler>()));

        this.RegisterHandler(1100, new StartLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<StartLiveHandler>()));
        this.RegisterHandler(1101, new StopLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<StopLiveHandler>()));
        this.RegisterHandler(1102, new InjectGenesHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<InjectGenesHandler>()));
        this.RegisterHandler(1105, new GetLiveStateHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetLiveStateHandler>()));

        this.RegisterHandler(1200, new RunBacktestHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<RunBacktestHandler>()));
        this.RegisterHandler(1201, new CancelBacktestHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<CancelBacktestHandler>()));
        this.RegisterHandler(1202, new GetBacktestResultHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetBacktestResultHandler>()));
        this.RegisterHandler(1203, new ListBacktestsHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ListBacktestsHandler>()));

        this.RegisterHandler(1300, new StartOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<StartOptimizationHandler>()));
        this.RegisterHandler(1301, new CancelOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<CancelOptimizationHandler>()));
        this.RegisterHandler(1302, new PauseOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<PauseOptimizationHandler>()));
        this.RegisterHandler(1303, new ResumeOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ResumeOptimizationHandler>()));
        this.RegisterHandler(1304, new GetOptimizationStateHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetOptimizationStateHandler>()));
        this.RegisterHandler(1305, new GetOptimizationResultHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetOptimizationResultHandler>()));
        this.RegisterHandler(1306, new ListOptimizationsHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ListOptimizationsHandler>()));

        this.RegisterHandler(1400, new ReloadExtensionsHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<ReloadExtensionsHandler>()));
        this.RegisterHandler(1401, new DeployExtensionHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<DeployExtensionHandler>()));
        this.RegisterHandler(1402, new RemoveExtensionHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<RemoveExtensionHandler>()));
        this.RegisterHandler(1403, new ListExtensionsHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<ListExtensionsHandler>()));
        this.RegisterHandler(1404, new ActivateExtensionsHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<ActivateExtensionsHandler>()));

        this.RegisterHandler(1500, new GenerateReportHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GenerateReportHandler>()));
        this.RegisterHandler(1501, new GetReportHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetReportHandler>()));

        this.RegisterHandler(1600, new GetLogsHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetLogsHandler>()));
        this.RegisterHandler(1601, new DeleteLogsAllHandler(_cloudConnector, this, _loggerFactory.CreateLogger<DeleteLogsAllHandler>()));
        this.RegisterHandler(1602, new DeleteLogsExpiredHandler(_cloudConnector, this, _loggerFactory.CreateLogger<DeleteLogsExpiredHandler>()));
        this.RegisterHandler(1603, new SetLogLevelHandler(_cloudConnector, this, _loggerFactory.CreateLogger<SetLogLevelHandler>()));
        this.RegisterHandler(1604, new GetMetricsHandler(_cloudConnector, this, _telemetry, _loggerFactory.CreateLogger<GetMetricsHandler>()));
        this.RegisterHandler(1605, new ExportMetricsHandler(_cloudConnector, this, _telemetry, _loggerFactory.CreateLogger<ExportMetricsHandler>()));

        this.RegisterHandler(1700, new SetCronJobHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<SetCronJobHandler>()));
        this.RegisterHandler(1701, new DeleteCronJobHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<DeleteCronJobHandler>()));
        this.RegisterHandler(1702, new ListCronJobsHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<ListCronJobsHandler>()));
        this.RegisterHandler(1703, new SetScheduleHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<SetScheduleHandler>()));
        this.RegisterHandler(1704, new DeleteScheduleHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<DeleteScheduleHandler>()));
        this.RegisterHandler(1705, new ListSchedulesHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<ListSchedulesHandler>()));

        this.RegisterHandler(1800, new StartMiningHandler(_cloudConnector, this, _miningIntegration, _loggerFactory.CreateLogger<StartMiningHandler>()));
        this.RegisterHandler(1801, new StopMiningHandler(_cloudConnector, this, _miningIntegration, _loggerFactory.CreateLogger<StopMiningHandler>()));
        this.RegisterHandler(1802, new GetMiningStatusHandler(_cloudConnector, this, _miningIntegration, _loggerFactory.CreateLogger<GetMiningStatusHandler>()));
        this.RegisterHandler(1803, new UpdateMiningConfigHandler(_cloudConnector, this, _miningIntegration, _loggerFactory.CreateLogger<UpdateMiningConfigHandler>()));

        this.RegisterHandler(1900, new BroadcastMessageHandler(_cloudConnector, this, _loggerFactory.CreateLogger<BroadcastMessageHandler>()));
        this.RegisterHandler(1901, new SetAdminConfigHandler(_cloudConnector, this, _stateManager, _loggerFactory.CreateLogger<SetAdminConfigHandler>()));
        this.RegisterHandler(1902, new GetEngineCapabilitiesHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetEngineCapabilitiesHandler>()));
        this.RegisterHandler(1903, new GetEngineVersionHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetEngineVersionHandler>()));

        this.RegisterHandler(2000, new KillSwitchHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<KillSwitchHandler>()));
        this.RegisterHandler(2001, new EmergencyStopHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<EmergencyStopHandler>()));

        this.RegisterHandler(2100, new EnableBehaviorLoggingHandler(_cloudConnector, this, _behaviorRecorder, _loggerFactory.CreateLogger<EnableBehaviorLoggingHandler>()));
        this.RegisterHandler(2101, new DisableBehaviorLoggingHandler(_cloudConnector, this, _behaviorRecorder, _loggerFactory.CreateLogger<DisableBehaviorLoggingHandler>()));
        this.RegisterHandler(2102, new GetBehaviorLogsHandler(_cloudConnector, this, _behaviorRecorder, _loggerFactory.CreateLogger<GetBehaviorLogsHandler>()));
        this.RegisterHandler(2103, new DeleteBehaviorLogsHandler(_cloudConnector, this, _behaviorRecorder, _loggerFactory.CreateLogger<DeleteBehaviorLogsHandler>()));
    }

    public void Initialize()
    {
        if (_cloudConnector is CloudConnector connector)
        {
            connector.CommandReceived += this.OnCommandReceivedAsync;
        }
    }

    private async Task OnCommandReceivedAsync(CloudCommand command)
    {
        await this.DispatchAsync(command, CancellationToken.None).ConfigureAwait(false);
    }

    public override void RegisterHandler(int commandId, ICommandHandler handler)
    {
        _handlers[commandId] = handler;
        _logRegisterHandler(_logger, commandId, null);
    }

    public override async Task DispatchAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(command.CommandId, out var handler))
        {
            _logNoHandler(_logger, command.CommandId, null);
            await this.SendResponseAsync(command.CommandId, command.CorrelationId ?? string.Empty, null,
                $"Command {command.CommandId} not supported.", cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logCommandError(_logger, command.CommandId, ex);
            await this.SendResponseAsync(command.CommandId, command.CorrelationId ?? string.Empty, null,
                ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task SendResponseAsync(int commandId, string correlationId, object? result, string? error = null,
        CancellationToken cancellationToken = default)
    {
        if (!_cloudConnector.IsConnected)
        {
            _logCannotSendResponse(_logger, null);
            return;
        }

        var response = new CloudMessage
        {
            MessageType = "CommandResponse",
            Encrypted = true,
            CorrelationId = correlationId,
            Payload = new
            {
                CommandId = commandId,
                Status = string.IsNullOrEmpty(error) ? "Success" : "Error",
                Result = result,
                Error = error
            }
        };

        try
        {
            await _cloudConnector.SendAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logSendResponseError(_logger, ex);
        }
    }

    public override async Task StopUserTasksAsync(CancellationToken cancellationToken)
    {
        await _taskManager.StopAllUserTasksAsync(cancellationToken).ConfigureAwait(false);
    }
}
