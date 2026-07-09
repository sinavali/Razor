// -----------------------------------------------------------------------------
// <copyright file="RemoveExtensionHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Extensions;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class RemoveExtensionHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public RemoveExtensionHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<RemoveExtensionHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => CommandIds.RemoveExtension;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("Name", out object? nameObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Name.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string name = nameObj?.ToString()!;
        await _extensionManager.RemoveExtensionAsync(name, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Name = name }, cancellationToken).ConfigureAwait(false);
    }
}
