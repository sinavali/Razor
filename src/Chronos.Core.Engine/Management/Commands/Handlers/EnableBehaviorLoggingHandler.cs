using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Management.Commands.Handlers;

/// <summary>Handles the enable behavior logging command (2100).</summary>
internal sealed class EnableBehaviorLoggingHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;
    private readonly IExtensionManager _extensionManager;

    /// <summary>Initializes a new instance of the <see cref="EnableBehaviorLoggingHandler"/> class.</summary>
    public EnableBehaviorLoggingHandler(
        ICloudConnector cloudConnector,
        ICommandDispatcher dispatcher,
        IBehaviorRecorder behaviorRecorder,
        IExtensionManager extensionManager,
        ILogger<EnableBehaviorLoggingHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
        _extensionManager = extensionManager;
    }

    /// <inheritdoc/>
    public override int CommandId => CommandIds.EnableBehaviorLogging;

    /// <inheritdoc/>
    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("SessionId", out object? sessionIdObj) ||
            !dict.TryGetValue("StrategyName", out object? strategyNameObj) ||
            !dict.TryGetValue("Genes", out object? genesObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing SessionId, StrategyName, or Genes.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string sessionId = sessionIdObj?.ToString()!;
        string strategyName = strategyNameObj?.ToString()!;
        double[] genes = (genesObj as double[]) ?? Array.Empty<double>();

        int uploadInterval = AppConstants.DefaultBehaviorUploadIntervalSeconds;
        if (dict.TryGetValue("UploadIntervalSeconds", out object? intervalObj) && intervalObj is int interval &&
            interval > 0)
        {
            uploadInterval = interval;
        }

        int snapshotInterval = 10;
        if (dict.TryGetValue("SnapshotIntervalSeconds", out object? snapObj) && snapObj is int snap && snap > 0)
        {
            snapshotInterval = snap;
        }

        // Cast to concrete to call Enable
        if (_behaviorRecorder is BehaviorRecorder concrete)
        {
            concrete.Enable(sessionId, strategyName, genes, uploadInterval);
        }
        else
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "BehaviorRecorder not available.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        _extensionManager.EnableBehaviorLoggingOnStrategy(sessionId, snapshotInterval);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { SessionId = sessionId }, cancellationToken)
            .ConfigureAwait(false);
    }
}
