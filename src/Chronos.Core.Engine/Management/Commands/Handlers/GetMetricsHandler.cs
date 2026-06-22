// -----------------------------------------------------------------------------
// <copyright file="GetMetricsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class GetMetricsHandler : CommandHandlerBase
{
    private readonly IEngineTelemetry _telemetry;

    public GetMetricsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IEngineTelemetry telemetry, ILogger<GetMetricsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _telemetry = telemetry;
    }

    public override int CommandId => 1604;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object metrics = _telemetry.GetMetricsSnapshot();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, metrics, cancellationToken).ConfigureAwait(false);
    }
}
