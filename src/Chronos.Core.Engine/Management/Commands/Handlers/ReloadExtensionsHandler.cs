// -----------------------------------------------------------------------------
// <copyright file="ReloadExtensionsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

// ─── Extensions ──────────────────────────────────────────────────

internal sealed class ReloadExtensionsHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public ReloadExtensionsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<ReloadExtensionsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => 1400;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        await _extensionManager.ReloadExtensionsAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Extensions reloaded." }, cancellationToken).ConfigureAwait(false);
    }
}
