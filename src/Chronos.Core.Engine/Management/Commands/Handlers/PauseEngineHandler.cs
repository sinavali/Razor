// -----------------------------------------------------------------------------
// <copyright file="PauseEngineHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for pausing non-critical tasks.</summary>
internal sealed class PauseEngineHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, Exception?> _logPausingEngine =
        LoggerMessage.Define(LogLevel.Information, 0, "Pausing non-critical tasks.");

    public PauseEngineHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ILogger<PauseEngineHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.PauseEngine;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logPausingEngine(Logger, null);

        // Pause all backtest and optimisation tasks, but leave live trading running.
        foreach (var task in _taskManager.AllTasks)
        {
            if (task.TaskType != "Live" && task.State == TaskState.Running)
            {
                await task.PauseAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Non-critical tasks paused." }, cancellationToken)
            .ConfigureAwait(false);
    }
}
