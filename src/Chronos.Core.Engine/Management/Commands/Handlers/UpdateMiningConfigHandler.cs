// -----------------------------------------------------------------------------
// <copyright file="UpdateMiningConfigHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.Mining;
using Microsoft.Extensions.Logging;

internal sealed class UpdateMiningConfigHandler : CommandHandlerBase
{
    private readonly IMiningIntegration _miningIntegration;

    public UpdateMiningConfigHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IMiningIntegration miningIntegration, ILogger<UpdateMiningConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _miningIntegration = miningIntegration;
    }

    public override int CommandId => 1803;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object config = command.Parameters ?? new object();
        await _miningIntegration.UpdateConfigAsync(config, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Mining config updated." }, cancellationToken).ConfigureAwait(false);
    }
}
