// -----------------------------------------------------------------------------
// <copyright file="BroadcastMessageHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

// ─── Admin & Broadcast ─────────────────────────────────────────

internal sealed class BroadcastMessageHandler : CommandHandlerBase
{
    private static readonly Action<ILogger, Exception?> _logBroadcastMessageWarning =
        LoggerMessage.Define(LogLevel.Warning, 0, "Broadcast message displayed.");

    public BroadcastMessageHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger<BroadcastMessageHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
    }

    public override int CommandId => CommandIds.BroadcastMessage;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("Text", out object? textObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Text.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string text = textObj?.ToString() ?? string.Empty;
        string style = dict.TryGetValue("Style", out object? styleObj) ? styleObj?.ToString() ?? "info" : "info";

        Console.ForegroundColor = style switch
        {
            "error" => ConsoleColor.Red,
            "warning" => ConsoleColor.Yellow,
            "success" => ConsoleColor.Green,
            _ => ConsoleColor.Cyan
        };
        Console.WriteLine($"\n=== BROADCAST ===\n{text}\n================\n");
        Console.ResetColor();

        _logBroadcastMessageWarning(Logger, null);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Broadcast displayed." }, cancellationToken).ConfigureAwait(false);
    }
}
