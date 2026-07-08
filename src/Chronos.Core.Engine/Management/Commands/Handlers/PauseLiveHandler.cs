// -----------------------------------------------------------------------------
// <copyright file="PauseLiveHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for pausing live trading (rejects new orders).</summary>
internal sealed class PauseLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, string, Exception?> _logPausingLive =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Pausing live trading for task {TaskId}.");

    public PauseLiveHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ILogger<PauseLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.PauseLive;

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
        _logPausingLive(Logger, taskId, null);

        await _taskManager.PauseTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId, State = "Paused" }, cancellationToken)
            .ConfigureAwait(false);
    }
}
