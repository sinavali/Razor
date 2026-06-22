// -----------------------------------------------------------------------------
// <copyright file="EmergencyStopHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
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

    public override int CommandId => 2001;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logEmergencyStop(Logger, null);
        await _taskManager.StopAllTasksAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Emergency stop executed." }, cancellationToken).ConfigureAwait(false);
    }
}
