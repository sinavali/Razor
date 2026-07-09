// -----------------------------------------------------------------------------
// <copyright file="ShutdownHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class ShutdownHandler : CommandHandlerBase
{
    private static readonly Action<ILogger, Exception?> _logShutdownWarning =
        LoggerMessage.Define(LogLevel.Warning, 0, "Shutdown command received. Initiating shutdown...");

    public ShutdownHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<ShutdownHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.Shutdown;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logShutdownWarning(Logger, null);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Shutdown initiated." }, cancellationToken).ConfigureAwait(false);
        Environment.Exit(0);
    }
}
