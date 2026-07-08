// -----------------------------------------------------------------------------
// <copyright file="CommandDispatcher.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Commands.Handlers;
using Chronos.Core.Engine.Management.Scheduling;
using Chronos.Core.Engine.Management.Tasks;
using Chronos.Core.Sdk.Shared;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    private readonly IStateManager _stateManager;
    private readonly IBehaviorRecorder _behaviorRecorder;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConfigStore _configStore;

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
    public CommandDispatcher(
        ICloudConnector cloudConnector,
        ILogger<CommandDispatcher> logger,
        ITaskManager taskManager,
        IExtensionManager extensionManager,
        IEngineTelemetry telemetry,
        ICronJobManager cronJobManager,
        IStateManager stateManager,
        IBehaviorRecorder behaviorRecorder,
        ILoggerFactory loggerFactory,
        ConfigStore configStore)
    {
        _cloudConnector = cloudConnector;
        _logger = logger;
        _taskManager = taskManager;
        _extensionManager = extensionManager;
        _telemetry = telemetry;
        _cronJobManager = cronJobManager;
        _stateManager = stateManager;
        _behaviorRecorder = behaviorRecorder;
        _loggerFactory = loggerFactory;
        _configStore = configStore;
        this.RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        // System Management (1000-1099)
        this.RegisterHandler(CommandIds.GetStatus, new GetStatusHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetStatusHandler>(), _taskManager));
        this.RegisterHandler(CommandIds.PauseEngine, new PauseEngineHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<PauseEngineHandler>()));
        this.RegisterHandler(CommandIds.ResumeEngine, new ResumeEngineHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ResumeEngineHandler>()));
        this.RegisterHandler(CommandIds.Shutdown, new ShutdownHandler(_cloudConnector, this, _loggerFactory.CreateLogger<ShutdownHandler>()));
        this.RegisterHandler(CommandIds.Restart, new RestartHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<RestartHandler>()));
        this.RegisterHandler(CommandIds.SetConfig, new SetConfigHandler(_cloudConnector, this, _configStore, _loggerFactory.CreateLogger<SetConfigHandler>()));
        this.RegisterHandler(CommandIds.GetConfig, new GetConfigHandler(_cloudConnector, this, _configStore, _loggerFactory.CreateLogger<GetConfigHandler>()));
        this.RegisterHandler(CommandIds.GetCapabilities, new GetCapabilitiesHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetCapabilitiesHandler>()));

        // Live Trading (1100-1199)
        this.RegisterHandler(CommandIds.StartLive, new StartLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<StartLiveHandler>()));
        this.RegisterHandler(CommandIds.StopLive, new StopLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<StopLiveHandler>()));
        this.RegisterHandler(CommandIds.InjectGenes, new InjectGenesHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<InjectGenesHandler>()));
        this.RegisterHandler(CommandIds.PauseLive, new PauseLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<PauseLiveHandler>()));
        this.RegisterHandler(CommandIds.ResumeLive, new ResumeLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ResumeLiveHandler>()));
        this.RegisterHandler(CommandIds.GetLiveState, new GetLiveStateHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetLiveStateHandler>()));
        this.RegisterHandler(CommandIds.SyncLive, new SyncLiveHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<SyncLiveHandler>()));
        this.RegisterHandler(CommandIds.SetLiveConfig, new SetLiveConfigHandler(_cloudConnector, this, _taskManager, _configStore, _loggerFactory.CreateLogger<SetLiveConfigHandler>()));
        this.RegisterHandler(CommandIds.GetLiveMetrics, new GetLiveMetricsHandler(_cloudConnector, this, _taskManager, _telemetry, _loggerFactory.CreateLogger<GetLiveMetricsHandler>()));

        // Backtesting (1200-1299)
        this.RegisterHandler(CommandIds.RunBacktest, new RunBacktestHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<RunBacktestHandler>()));
        this.RegisterHandler(CommandIds.CancelBacktest, new CancelBacktestHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<CancelBacktestHandler>()));
        this.RegisterHandler(CommandIds.GetBacktestResult, new GetBacktestResultHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetBacktestResultHandler>()));
        this.RegisterHandler(CommandIds.ListBacktests, new ListBacktestsHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ListBacktestsHandler>()));

        // Optimisation (1300-1399)
        this.RegisterHandler(CommandIds.StartOptimization, new StartOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<StartOptimizationHandler>()));
        this.RegisterHandler(CommandIds.CancelOptimization, new CancelOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<CancelOptimizationHandler>()));
        this.RegisterHandler(CommandIds.PauseOptimization, new PauseOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<PauseOptimizationHandler>()));
        this.RegisterHandler(CommandIds.ResumeOptimization, new ResumeOptimizationHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ResumeOptimizationHandler>()));
        this.RegisterHandler(CommandIds.GetOptimizationState, new GetOptimizationStateHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetOptimizationStateHandler>()));
        this.RegisterHandler(CommandIds.GetOptimizationResult, new GetOptimizationResultHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<GetOptimizationResultHandler>()));
        this.RegisterHandler(CommandIds.ListOptimizations, new ListOptimizationsHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<ListOptimizationsHandler>()));

        // Extensions (1400-1499)
        this.RegisterHandler(CommandIds.ReloadExtensions, new ReloadExtensionsHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<ReloadExtensionsHandler>()));
        this.RegisterHandler(CommandIds.DeployExtension, new DeployExtensionHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<DeployExtensionHandler>()));
        this.RegisterHandler(CommandIds.RemoveExtension, new RemoveExtensionHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<RemoveExtensionHandler>()));
        this.RegisterHandler(CommandIds.ListExtensions, new ListExtensionsHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<ListExtensionsHandler>()));
        this.RegisterHandler(CommandIds.ActivateExtensions, new ActivateExtensionsHandler(_cloudConnector, this, _extensionManager, _loggerFactory.CreateLogger<ActivateExtensionsHandler>()));

        // Reports (1500-1599)
        this.RegisterHandler(CommandIds.GenerateReport, new GenerateReportHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GenerateReportHandler>()));
        this.RegisterHandler(CommandIds.GetReport, new GetReportHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetReportHandler>()));

        // Logs & Telemetry (1600-1699)
        this.RegisterHandler(CommandIds.GetLogs, new GetLogsHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetLogsHandler>()));
        this.RegisterHandler(CommandIds.DeleteLogsAll, new DeleteLogsAllHandler(_cloudConnector, this, _loggerFactory.CreateLogger<DeleteLogsAllHandler>()));
        this.RegisterHandler(CommandIds.DeleteLogsExpired, new DeleteLogsExpiredHandler(_cloudConnector, this, _loggerFactory.CreateLogger<DeleteLogsExpiredHandler>()));
        this.RegisterHandler(CommandIds.SetLogLevel, new SetLogLevelHandler(_cloudConnector, this, _loggerFactory.CreateLogger<SetLogLevelHandler>()));
        this.RegisterHandler(CommandIds.GetMetrics, new GetMetricsHandler(_cloudConnector, this, _telemetry, _loggerFactory.CreateLogger<GetMetricsHandler>()));
        this.RegisterHandler(CommandIds.ExportMetrics, new ExportMetricsHandler(_cloudConnector, this, _telemetry, _loggerFactory.CreateLogger<ExportMetricsHandler>()));

        // Schedules & Cron (1700-1799)
        this.RegisterHandler(CommandIds.SetCronJob, new SetCronJobHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<SetCronJobHandler>()));
        this.RegisterHandler(CommandIds.DeleteCronJob, new DeleteCronJobHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<DeleteCronJobHandler>()));
        this.RegisterHandler(CommandIds.ListCronJobs, new ListCronJobsHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<ListCronJobsHandler>()));
        this.RegisterHandler(CommandIds.SetSchedule, new SetScheduleHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<SetScheduleHandler>()));
        this.RegisterHandler(CommandIds.DeleteSchedule, new DeleteScheduleHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<DeleteScheduleHandler>()));
        this.RegisterHandler(CommandIds.ListSchedules, new ListSchedulesHandler(_cloudConnector, this, _cronJobManager, _loggerFactory.CreateLogger<ListSchedulesHandler>()));

        // Admin & Broadcast (1900-1999)
        this.RegisterHandler(CommandIds.BroadcastMessage, new BroadcastMessageHandler(_cloudConnector, this, _loggerFactory.CreateLogger<BroadcastMessageHandler>()));
        this.RegisterHandler(CommandIds.SetAdminConfig, new SetAdminConfigHandler(_cloudConnector, this, _stateManager, _loggerFactory.CreateLogger<SetAdminConfigHandler>()));
        this.RegisterHandler(CommandIds.GetEngineCapabilities, new GetEngineCapabilitiesHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetEngineCapabilitiesHandler>()));
        this.RegisterHandler(CommandIds.GetEngineVersion, new GetEngineVersionHandler(_cloudConnector, this, _loggerFactory.CreateLogger<GetEngineVersionHandler>()));

        // Kill & Emergency (2000-2099)
        this.RegisterHandler(CommandIds.KillSwitch, new KillSwitchHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<KillSwitchHandler>()));
        this.RegisterHandler(CommandIds.EmergencyStop, new EmergencyStopHandler(_cloudConnector, this, _taskManager, _loggerFactory.CreateLogger<EmergencyStopHandler>()));

        // Behavior Logging (2100-2199)
        this.RegisterHandler(CommandIds.EnableBehaviorLogging, new EnableBehaviorLoggingHandler(_cloudConnector, this, _behaviorRecorder, _extensionManager, _loggerFactory.CreateLogger<EnableBehaviorLoggingHandler>()));
        this.RegisterHandler(CommandIds.DisableBehaviorLogging, new DisableBehaviorLoggingHandler(_cloudConnector, this, _behaviorRecorder, _extensionManager, _loggerFactory.CreateLogger<DisableBehaviorLoggingHandler>()));
        this.RegisterHandler(CommandIds.GetBehaviorLogs, new GetBehaviorLogsHandler(_cloudConnector, this, _behaviorRecorder, _loggerFactory.CreateLogger<GetBehaviorLogsHandler>()));
        this.RegisterHandler(CommandIds.DeleteBehaviorLogs, new DeleteBehaviorLogsHandler(_cloudConnector, this, _behaviorRecorder, _loggerFactory.CreateLogger<DeleteBehaviorLogsHandler>()));
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
