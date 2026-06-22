// -----------------------------------------------------------------------------
// <copyright file="DisableBehaviorLoggingHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Microsoft.Extensions.Logging;

internal sealed class DisableBehaviorLoggingHandler : CommandHandlerBase
{
    private readonly IBehaviorRecorder _behaviorRecorder;

    public DisableBehaviorLoggingHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IBehaviorRecorder behaviorRecorder, ILogger<DisableBehaviorLoggingHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _behaviorRecorder = behaviorRecorder;
    }

    public override int CommandId => 2101;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        _behaviorRecorder.Disable();
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Behavior logging disabled." }, cancellationToken).ConfigureAwait(false);
    }
}
