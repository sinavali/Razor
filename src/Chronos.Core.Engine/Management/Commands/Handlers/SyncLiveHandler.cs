// -----------------------------------------------------------------------------
// <copyright file="SyncLiveHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for forcing a live state reconciliation.</summary>
internal sealed class SyncLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, string, Exception?> _logSyncingLive =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Syncing live state for task {TaskId}.");

    public SyncLiveHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ILogger<SyncLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1106;

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
        _logSyncingLive(Logger, taskId, null);

        var task = _taskManager.GetTask(taskId);
        if (task is not LiveTask liveTask)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, $"Task {taskId} is not a live task.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Get the current live state from the kernel service
        var state = await _taskManager.GetLiveStateAsync(taskId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId, State = state }, cancellationToken)
            .ConfigureAwait(false);
    }
}
