// -----------------------------------------------------------------------------
// <copyright file="EnableBehaviorLoggingHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Microsoft.Extensions.Logging;

// ─── Behavior Logging ──────────────────────────────────────────

internal sealed class EnableBehaviorLoggingHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public EnableBehaviorLoggingHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<EnableBehaviorLoggingHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2100;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj) ||
            !dict.TryGetValue("StrategyName", out object? strategyNameObj) ||
            !dict.TryGetValue("Genes", out object? genesObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId, StrategyName, or Genes.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        string strategyName = strategyNameObj?.ToString()!;
        double[] genes = (genesObj as double[]) ?? Array.Empty<double>();

        _behaviorRecorder.Enable(sessionId, strategyName, genes);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { SessionId = sessionId }, cancellationToken).ConfigureAwait(false);
    }
}
