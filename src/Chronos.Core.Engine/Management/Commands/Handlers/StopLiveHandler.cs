// -----------------------------------------------------------------------------
// <copyright file="StopLiveHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

internal sealed class StopLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public StopLiveHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<StopLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.StopLive;

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
