// -----------------------------------------------------------------------------
// <copyright file="DeleteLogsAllHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class DeleteLogsAllHandler : CommandHandlerBase
{
    private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static readonly Action<ILogger, string, Exception?> _logDeletedLogFile =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Deleted log file: {File}");

    private static readonly Action<ILogger, string, Exception?> _logDeleteLogError =
        LoggerMessage.Define<string>(LogLevel.Error, 1, "Error deleting log file: {File}");

    public DeleteLogsAllHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<DeleteLogsAllHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.DeleteLogsAll;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.GetFiles(_logDirectory, "Razor-*.log"))
        {
            try
            {
                File.Delete(file);
                _logDeletedLogFile(Logger, Path.GetFileName(file), null);
            }
            catch (Exception ex)
            {
                _logDeleteLogError(Logger, Path.GetFileName(file), ex);
            }
        }
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "All logs deleted." }, cancellationToken).ConfigureAwait(false);
    }
}
