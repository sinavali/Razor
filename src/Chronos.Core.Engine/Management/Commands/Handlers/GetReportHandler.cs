// -----------------------------------------------------------------------------
// <copyright file="GetReportHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class GetReportHandler : CommandHandlerBase
{
    public GetReportHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetReportHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => 1501;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("ReportId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ReportId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string reportId = idObj?.ToString()!;
        string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports", $"{reportId}.json");

        byte[] data = Array.Empty<byte>();
        if (File.Exists(reportPath))
        {
            data = await File.ReadAllBytesAsync(reportPath, cancellationToken).ConfigureAwait(false);
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ReportId = reportId, Data = Convert.ToBase64String(data) }, cancellationToken).ConfigureAwait(false);
    }
}
