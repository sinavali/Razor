// -----------------------------------------------------------------------------
// <copyright file="GetBehaviorLogsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Microsoft.Extensions.Logging;

internal sealed class GetBehaviorLogsHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public GetBehaviorLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<GetBehaviorLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2102;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        object logs = await _behaviorRecorder.GetLogsAsync(sessionId, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, logs, cancellationToken).ConfigureAwait(false);
    }
}
