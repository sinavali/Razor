// -----------------------------------------------------------------------------
// <copyright file="ExportMetricsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using System.Text.Json;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class ExportMetricsHandler : CommandHandlerBase
{
    private readonly IEngineTelemetry _telemetry;

    public ExportMetricsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IEngineTelemetry telemetry, ILogger<ExportMetricsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _telemetry = telemetry;
    }

    public override int CommandId => 1605;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        string metricsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"metrics_export_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        object metricsData = _telemetry.GetMetricsSnapshot();
        string json = JsonSerializer.Serialize(metricsData);

        await File.WriteAllTextAsync(metricsFile, json, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Metrics exported.", File = Path.GetFileName(metricsFile) }, cancellationToken).ConfigureAwait(false);
    }
}
