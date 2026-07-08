// -----------------------------------------------------------------------------
// <copyright file="StartOptimizationHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

// ─── Optimization ────────────────────────────────────────────────

internal sealed class StartOptimizationHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public StartOptimizationHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<StartOptimizationHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.StartOptimization;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object config = command.Parameters ?? new object();
        string taskId = await _taskManager.StartOptimizationTaskAsync(config, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}
