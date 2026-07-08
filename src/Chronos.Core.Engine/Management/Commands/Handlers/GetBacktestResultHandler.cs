// -----------------------------------------------------------------------------
// <copyright file="GetBacktestResultHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

internal sealed class GetBacktestResultHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetBacktestResultHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<GetBacktestResultHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.GetBacktestResult;

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
