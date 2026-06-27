// -----------------------------------------------------------------------------
// <copyright file="GetConfigHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

/// <summary>Handler for retrieving runtime configuration.</summary>
internal sealed class GetConfigHandler : CommandHandlerBase
{
    private readonly ConfigStore _configStore;

    public GetConfigHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ConfigStore configStore,
        ILogger<GetConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _configStore = configStore;
    }

    public override int CommandId => 1009;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        // If a specific key is requested, return just that value
        if (command.Parameters is Dictionary<string, object> dict &&
            dict.TryGetValue("Key", out object? keyObj) &&
            keyObj is string key)
        {
            var value = _configStore.Get(key);
            await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Key = key, Value = value }, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Otherwise return all config
        var allConfig = _configStore.GetAll();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Config = allConfig }, cancellationToken)
            .ConfigureAwait(false);
    }
}
