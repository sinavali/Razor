// -----------------------------------------------------------------------------
// <copyright file="StartLiveHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

// ─── Live Trading ────────────────────────────────────────────────

internal sealed class StartLiveHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public StartLiveHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<StartLiveHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1100;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var config = command.Parameters ?? new object();
        string taskId = await _taskManager.StartLiveTaskAsync(config, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { TaskId = taskId }, cancellationToken).ConfigureAwait(false);
    }
}
