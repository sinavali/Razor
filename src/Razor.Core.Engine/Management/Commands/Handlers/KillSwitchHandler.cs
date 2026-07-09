// -----------------------------------------------------------------------------
// <copyright file="KillSwitchHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

// ─── Kill & Emergency ──────────────────────────────────────────

internal sealed class KillSwitchHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, Exception?> _logKillSwitchActivated =
        LoggerMessage.Define(LogLevel.Warning, 0, "KILL SWITCH ACTIVATED!");

    public KillSwitchHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<KillSwitchHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => CommandIds.KillSwitch;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logKillSwitchActivated(Logger, null);
        await _taskManager.StopAllTasksAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Kill switch activated. All tasks stopped." }, cancellationToken).ConfigureAwait(false);

        Environment.Exit(1);
    }
}
