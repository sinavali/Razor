// -----------------------------------------------------------------------------
// <copyright file="GetMetricsHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Core;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class GetMetricsHandler : CommandHandlerBase
{
    private readonly IEngineTelemetry _telemetry;

    public GetMetricsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IEngineTelemetry telemetry, ILogger<GetMetricsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _telemetry = telemetry;
    }

    public override int CommandId => CommandIds.GetMetrics;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object metrics = _telemetry.GetMetricsSnapshot();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, metrics, cancellationToken).ConfigureAwait(false);
    }
}
