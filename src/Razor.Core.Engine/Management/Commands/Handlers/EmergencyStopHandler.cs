// -----------------------------------------------------------------------------
// <copyright file="EmergencyStopHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

internal sealed class EmergencyStopHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, Exception?> _logEmergencyStop =
        LoggerMessage.Define(LogLevel.Warning, 0, "EMERGENCY STOP!");

    public EmergencyStopHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<EmergencyStopHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.EmergencyStop;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logEmergencyStop(Logger, null);
        await _taskManager.StopAllTasksAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Emergency stop executed." }, cancellationToken).ConfigureAwait(false);
    }
}
