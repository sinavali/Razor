// -----------------------------------------------------------------------------
// <copyright file="GetEngineVersionHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class GetEngineVersionHandler : CommandHandlerBase
{
    public GetEngineVersionHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetEngineVersionHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.GetEngineVersion;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Version = AppConstants.EngineVersion }, cancellationToken).ConfigureAwait(false);
    }
}
