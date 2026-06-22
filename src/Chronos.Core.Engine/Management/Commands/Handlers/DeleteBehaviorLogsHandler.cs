// -----------------------------------------------------------------------------
// <copyright file="DeleteBehaviorLogsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Microsoft.Extensions.Logging;

internal sealed class DeleteBehaviorLogsHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public DeleteBehaviorLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<DeleteBehaviorLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2103;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        await _behaviorRecorder.DeleteLogsAsync(sessionId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { SessionId = sessionId }, cancellationToken).ConfigureAwait(false);
    }
}
