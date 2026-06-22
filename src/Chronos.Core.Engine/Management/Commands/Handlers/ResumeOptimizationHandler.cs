// -----------------------------------------------------------------------------
// <copyright file="ResumeOptimizationHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

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
