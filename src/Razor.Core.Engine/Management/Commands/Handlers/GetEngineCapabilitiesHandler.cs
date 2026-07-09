// -----------------------------------------------------------------------------
// <copyright file="GetEngineCapabilitiesHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Core;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class GetEngineCapabilitiesHandler : CommandHandlerBase
{
    public GetEngineCapabilitiesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetEngineCapabilitiesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.GetEngineCapabilities;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var result = new
        {
            EngineVersion = AppConstants.EngineVersion,
            Capabilities = AppConstants.SupportedCapabilities
        };
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, result, cancellationToken).ConfigureAwait(false);
    }
}
