// -----------------------------------------------------------------------------
// <copyright file="SetAdminConfigHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using System.Text.Json;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class SetAdminConfigHandler : CommandHandlerBase
{
    private readonly IStateManager _stateManager;

    public SetAdminConfigHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IStateManager stateManager, ILogger<SetAdminConfigHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _stateManager = stateManager;
    }

    public override int CommandId => 1901;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        string configJson = JsonSerializer.Serialize(command.Parameters);
        await _stateManager.SetMetadataAsync("AdminConfig", configJson, cancellationToken).ConfigureAwait(false);

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Admin config updated." }, cancellationToken).ConfigureAwait(false);
    }
}
