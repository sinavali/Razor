// -----------------------------------------------------------------------------
// <copyright file="GetMiningStatusHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.Mining;
using Microsoft.Extensions.Logging;

internal sealed class GetMiningStatusHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public GetMiningStatusHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<GetMiningStatusHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1802;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object status = await _miningIntegration.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, status, cancellationToken).ConfigureAwait(false);
    }
}
