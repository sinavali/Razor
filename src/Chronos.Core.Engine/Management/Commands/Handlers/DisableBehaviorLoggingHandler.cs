using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Chronos.Core.Sdk.Shared;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Commands.Handlers;

/// <summary>Handles the disable behavior logging command (2101).</summary>
internal sealed class DisableBehaviorLoggingHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;
    private readonly IExtensionManager _extensionManager;

    /// <summary>Initializes a new instance of the <see cref="DisableBehaviorLoggingHandler"/> class.</summary>
    public DisableBehaviorLoggingHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        IBehaviorRecorder behaviorRecorder,
        IExtensionManager extensionManager,
        ILogger<DisableBehaviorLoggingHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
        _extensionManager = extensionManager;
    }

    /// <inheritdoc/>
    public override int CommandId => CommandIds.DisableBehaviorLogging;

    /// <inheritdoc/>
    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _extensionManager.DisableBehaviorLoggingOnStrategy();

        if (_behaviorRecorder is BehaviorRecorder concrete)
        {
            concrete.Disable();
        }

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Behavior logging disabled." }, cancellationToken)
            .ConfigureAwait(false);
    }
}
