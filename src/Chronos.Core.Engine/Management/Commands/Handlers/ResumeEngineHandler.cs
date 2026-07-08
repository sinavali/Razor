// -----------------------------------------------------------------------------
// <copyright file="ResumeEngineHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for resuming paused tasks.</summary>
internal sealed class ResumeEngineHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, Exception?> _logResumingEngine =
        LoggerMessage.Define(LogLevel.Information, 0, "Resuming paused tasks.");

    public ResumeEngineHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ILogger<ResumeEngineHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.ResumeEngine;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logResumingEngine(Logger, null);

        foreach (var task in _taskManager.AllTasks)
        {
            if (task.TaskType != "Live" && task.State == TaskState.Paused)
            {
                await task.ResumeAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Paused tasks resumed." }, cancellationToken)
            .ConfigureAwait(false);
    }
}
