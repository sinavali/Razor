// -----------------------------------------------------------------------------
// <copyright file="DeleteLogsExpiredHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;
using System.Globalization;

internal sealed class DeleteLogsExpiredHandler : CommandHandlerBase
{
    private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static readonly Action<ILogger, string, Exception?> _logDeleteExpiredInfo =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Deleted expired log: {File}");

    private static readonly Action<ILogger, string, Exception?> _logDeleteExpiredError =
        LoggerMessage.Define<string>(LogLevel.Error, 1, "Error deleting log: {File}");

    public DeleteLogsExpiredHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<DeleteLogsExpiredHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.DeleteLogsExpired;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-30);
        foreach (string file in Directory.GetFiles(_logDirectory, "chronos-*.log"))
        {
            string fileName = Path.GetFileName(file);
            string datePart = fileName.Substring(8, 10);
            if (DateTime.TryParse(datePart, CultureInfo.InvariantCulture, out DateTime fileDate) && fileDate < cutoff)
            {
                try
                {
                    File.Delete(file);
                    _logDeleteExpiredInfo(Logger, fileName, null);
                }
                catch (Exception ex)
                {
                    _logDeleteExpiredError(Logger, fileName, ex);
                }
            }
        }
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Expired logs deleted." }, cancellationToken).ConfigureAwait(false);
    }
}
