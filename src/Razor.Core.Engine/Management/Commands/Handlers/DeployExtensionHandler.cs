// -----------------------------------------------------------------------------
// <copyright file="DeployExtensionHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Extensions;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

internal sealed class DeployExtensionHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    public DeployExtensionHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<DeployExtensionHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => CommandIds.DeployExtension;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("Name", out object? nameObj) ||
            !dict.TryGetValue("BinaryData", out object? dataObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing Name or BinaryData.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string name = nameObj?.ToString()!;
        byte[] data = dataObj as byte[] ?? Array.Empty<byte>();

        await _extensionManager.DeployExtensionAsync(name, data, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Name = name }, cancellationToken).ConfigureAwait(false);
    }
}
