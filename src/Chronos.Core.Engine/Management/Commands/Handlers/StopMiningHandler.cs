// -----------------------------------------------------------------------------
// <copyright file="StopMiningHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.Mining;
using Microsoft.Extensions.Logging;

internal sealed class StopMiningHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public StopMiningHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<StopMiningHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1801;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        await _miningIntegration.StopAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Mining stopped." }, cancellationToken).ConfigureAwait(false);
    }
}
