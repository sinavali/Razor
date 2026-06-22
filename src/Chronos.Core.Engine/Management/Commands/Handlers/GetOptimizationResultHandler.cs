// -----------------------------------------------------------------------------
// <copyright file="GetOptimizationResultHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

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
