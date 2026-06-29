// -----------------------------------------------------------------------------
// <copyright file="RestartHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>Handler for gracefully restarting the engine.</summary>
internal sealed class RestartHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;
    private readonly ICloudConnector _cloudConnector;

    private static readonly Action<ILogger, Exception?> _logRestarting =
        LoggerMessage.Define(LogLevel.Warning, 0, "Restarting engine...");

    private static readonly Action<ILogger, Exception?> _logRestartFailed =
        LoggerMessage.Define(LogLevel.Error, 1, "Failed to restart engine.");

    public RestartHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        ITaskManager taskManager,
        ILogger<RestartHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
        _cloudConnector = cloudConnector;
    }

    public override int CommandId => 1007;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _logRestarting(Logger, null);

        // Send acknowledgment before restarting
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Restarting engine..." }, cancellationToken)
            .ConfigureAwait(false);

        // Give the response a moment to send
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        // Stop all tasks gracefully
        await _taskManager.StopAllTasksAsync(cancellationToken).ConfigureAwait(false);

        // Disconnect from cloud
        await _cloudConnector.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        // Restart the process
        var processPath = Environment.ProcessPath!;
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        // Pass credentials to the new process
        if (Credentials.IsAvailable)
        {
            startInfo.Arguments = $"--auth={Credentials.Username},{Credentials.Password},{Credentials.InstanceApiKey}";
        }

        using var newProcess = Process.Start(startInfo);
        if (newProcess == null)
        {
            _logRestartFailed(Logger, null);
            return;
        }

        // Exit current process
        Environment.Exit(0);
    }
}
