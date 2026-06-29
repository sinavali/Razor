using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Commands.Handlers;

/// <summary>Handles the get behavior logs command (2102).</summary>
internal sealed class GetBehaviorLogsHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    /// <summary>Initializes a new instance of the <see cref="GetBehaviorLogsHandler"/> class.</summary>
    public GetBehaviorLogsHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        IBehaviorRecorder behaviorRecorder,
        ILogger<GetBehaviorLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    /// <inheritdoc/>
    public override int CommandId => 2102;

    /// <inheritdoc/>
    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;

        object logs;
        if (_behaviorRecorder is BehaviorRecorder concrete)
        {
            logs = await concrete.GetLogsAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "BehaviorRecorder not available.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, logs, cancellationToken)
            .ConfigureAwait(false);
    }
}
