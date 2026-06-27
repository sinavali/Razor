// -----------------------------------------------------------------------------
// <copyright file="SetLiveConfigHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for updating live trading configuration.</summary>
internal sealed class SetLiveConfigHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;
    private readonly ConfigStore _configStore;

    private static readonly Action<ILogger, string, Exception?> _logLiveConfigUpdated =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Live config updated for task {TaskId}.");

    public SetLiveConfigHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ConfigStore configStore,
        ILogger<SetLiveConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
        _configStore = configStore;
    }

    public override int CommandId => 1107;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Invalid parameters: expected dictionary.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;
        _logLiveConfigUpdated(Logger, taskId, null);

        var task = _taskManager.GetTask(taskId);
        if (task is not LiveTask liveTask)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, $"Task {taskId} is not a live task.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Update live configuration in the config store
        var updatedConfig = new Dictionary<string, object>();

        if (dict.TryGetValue("StopOutLevel", out object? stopOutObj))
        {
            var stopOut = Convert.ToDouble(stopOutObj, System.Globalization.CultureInfo.InvariantCulture);
            _configStore.Set($"Live_{taskId}_StopOutLevel", stopOut);
            updatedConfig["StopOutLevel"] = stopOut;
        }

        if (dict.TryGetValue("MaxOpenPositions", out object? maxPosObj))
        {
            var maxPos = Convert.ToInt32(maxPosObj, System.Globalization.CultureInfo.InvariantCulture);
            _configStore.Set($"Live_{taskId}_MaxOpenPositions", maxPos);
            updatedConfig["MaxOpenPositions"] = maxPos;
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId, UpdatedConfig = updatedConfig }, cancellationToken)
            .ConfigureAwait(false);
    }
}
