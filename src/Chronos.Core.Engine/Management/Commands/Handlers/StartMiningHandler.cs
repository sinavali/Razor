// -----------------------------------------------------------------------------
// <copyright file="StartMiningHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.Mining;
using Microsoft.Extensions.Logging;

// ─── Mining ──────────────────────────────────────────────────────

internal sealed class StartMiningHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public StartMiningHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<StartMiningHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1800;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object config = command.Parameters ?? new object();
        await _miningIntegration.StartAsync(config, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Mining started." }, cancellationToken).ConfigureAwait(false);
    }
}
