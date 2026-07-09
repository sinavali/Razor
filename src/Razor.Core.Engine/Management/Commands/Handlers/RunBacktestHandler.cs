// -----------------------------------------------------------------------------
// <copyright file="RunBacktestHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

// ─── Backtesting ──────────────────────────────────────────────────

internal sealed class RunBacktestHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public RunBacktestHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<RunBacktestHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.RunBacktest;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object input = command.Parameters ?? new object();
        string taskId = await _taskManager.StartBacktestTaskAsync(input, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}
