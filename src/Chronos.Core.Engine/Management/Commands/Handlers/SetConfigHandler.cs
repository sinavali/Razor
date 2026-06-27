// -----------------------------------------------------------------------------
// <copyright file="SetConfigHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

/// <summary>Handler for updating runtime configuration.</summary>
internal sealed class SetConfigHandler : CommandHandlerBase
{
    private readonly ConfigStore _configStore;

    private static readonly Action<ILogger, string, string, Exception?> _logConfigUpdated =
        LoggerMessage.Define<string, string>(LogLevel.Information, 0, "Config updated: {Key} = {Value}");

    public SetConfigHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ConfigStore configStore,
        ILogger<SetConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _configStore = configStore;
    }

    public override int CommandId => 1008;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Invalid parameters: expected dictionary.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var result = new Dictionary<string, object?>();

        foreach (var kv in dict)
        {
            _configStore.Set(kv.Key, kv.Value);
            result[kv.Key] = kv.Value;
            _logConfigUpdated(Logger, kv.Key, kv.Value?.ToString() ?? "null", null);
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Config = result }, cancellationToken)
            .ConfigureAwait(false);
    }
}
