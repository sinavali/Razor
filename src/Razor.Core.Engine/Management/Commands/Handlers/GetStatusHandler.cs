// -----------------------------------------------------------------------------
// <copyright file="GetStatusHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Core;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

// ─── System Management ──────────────────────────────────────────

internal sealed class GetStatusHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public GetStatusHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetStatusHandler> logger, ITaskManager taskManager)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.GetStatus;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var status = new
        {
            EngineVersion = AppConstants.EngineVersion,
            IsConnected = CloudConnector.IsConnected,
            SessionId = CloudConnector.SessionId,
            Uptime = TimeSpan.Zero,
            Tasks = new
            {
                Running = _taskManager.RunningTasks.Count,
                Total = _taskManager.AllTasks.Count
            }
        };
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, status, cancellationToken).ConfigureAwait(false);
    }
}
