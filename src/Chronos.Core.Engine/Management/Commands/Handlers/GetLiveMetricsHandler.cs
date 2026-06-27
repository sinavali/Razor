// -----------------------------------------------------------------------------
// <copyright file="GetLiveMetricsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Kernel;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for retrieving live performance metrics.</summary>
internal sealed class GetLiveMetricsHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;
    private readonly IEngineTelemetry _telemetry;

    public GetLiveMetricsHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        IEngineTelemetry telemetry,
        ILogger<GetLiveMetricsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
        _telemetry = telemetry;
    }

    public override int CommandId => 1108;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("TaskId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing TaskId.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string taskId = idObj?.ToString()!;

        var task = _taskManager.GetTask(taskId);
        if (task is not LiveTask liveTask)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, $"Task {taskId} is not a live task.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Get live state
        var state = await _taskManager.GetLiveStateAsync(taskId, cancellationToken).ConfigureAwait(false);

        // Get telemetry snapshot
        var telemetrySnapshot = _telemetry.GetMetricsSnapshot();

        var metrics = new
        {
            TaskId = taskId,
            State = state,
            Telemetry = telemetrySnapshot,
            LastTickTime = liveTask.LastTickTime,
            StartTime = liveTask.StartTime,
            Uptime = DateTime.UtcNow - liveTask.StartTime
        };

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, metrics, cancellationToken)
            .ConfigureAwait(false);
    }
}
