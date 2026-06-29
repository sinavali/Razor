using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Kernel.Behavior;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Commands.Handlers;

/// <summary>Handles the delete behavior logs command (2103).</summary>
internal sealed class DeleteBehaviorLogsHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    /// <summary>Initializes a new instance of the <see cref="DeleteBehaviorLogsHandler"/> class.</summary>
    public DeleteBehaviorLogsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<DeleteBehaviorLogsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    /// <inheritdoc/>
    public override int CommandId => 2103;

    /// <inheritdoc/>
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
