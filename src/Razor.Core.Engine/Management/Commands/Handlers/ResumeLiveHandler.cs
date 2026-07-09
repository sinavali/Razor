// -----------------------------------------------------------------------------
// <copyright file="ResumeLiveHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Handler for resuming live trading.</summary>
internal sealed class ResumeLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, string, Exception?> _logResumingLive =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Resuming live trading for task {TaskId}.");

    public ResumeLiveHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ILogger<ResumeLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.ResumeLive;

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
        _logResumingLive(Logger, taskId, null);

        await _taskManager.ResumeTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId, State = "Resumed" }, cancellationToken)
            .ConfigureAwait(false);
    }
}
