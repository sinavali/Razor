// -----------------------------------------------------------------------------
// <copyright file="GetLogsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;
using System.Globalization;

// ─── Logs ──────────────────────────────────────────────────────────

internal sealed class GetLogsHandler : CommandHandlerBase
{
    private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    public GetLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<GetLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.GetLogs;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("LogType", out object? typeObj) ||
            !dict.TryGetValue("Date", out object? dateObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing LogType or Date.", cancellationToken).ConfigureAwait(false);
            return;
        }

        DateTime date = DateTime.Parse(dateObj?.ToString()!, CultureInfo.InvariantCulture);
        string pattern = $"chronos-{date:yyyy-MM-dd}-*.log";
        string[] files = Directory.GetFiles(_logDirectory, pattern);
        string? logFile = files.FirstOrDefault();

        if (logFile == null)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Log file not found.", cancellationToken).ConfigureAwait(false);
            return;
        }

        byte[] data = await File.ReadAllBytesAsync(logFile, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new
        {
            FileName = Path.GetFileName(logFile),
            Size = data.Length,
            Data = Convert.ToBase64String(data)
        }, cancellationToken).ConfigureAwait(false);
    }
}
