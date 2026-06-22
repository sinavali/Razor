// -----------------------------------------------------------------------------
// <copyright file="GenerateReportHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

// ─── Reports ──────────────────────────────────────────────────────

internal sealed class GenerateReportHandler : CommandHandlerBase
{
    public GenerateReportHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GenerateReportHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1500;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("ReportType", out object? typeObj) ||
            !dict.TryGetValue("Data", out object? dataObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ReportType or Data.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string reportType = typeObj?.ToString() ?? "json";
        string reportId = $"rpt_{Guid.NewGuid():N}";
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ReportId = reportId, Format = reportType }, cancellationToken).ConfigureAwait(false);
    }
}
