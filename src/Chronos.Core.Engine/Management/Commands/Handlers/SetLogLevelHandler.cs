// -----------------------------------------------------------------------------
// <copyright file="SetLogLevelHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class SetLogLevelHandler : CommandHandlerBase
{
    private static readonly Action<ILogger, string, Exception?> _logSetLogLevel =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Log level set to {Level}.");

    public SetLogLevelHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<SetLogLevelHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1603;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("Level", out object? levelObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Level.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string level = levelObj?.ToString() ?? "Information";
        _logSetLogLevel(Logger, level, null);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Level = level }, cancellationToken).ConfigureAwait(false);
    }
}
