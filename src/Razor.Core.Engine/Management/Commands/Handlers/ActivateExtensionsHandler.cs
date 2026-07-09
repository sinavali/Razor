// -----------------------------------------------------------------------------
// <copyright file="ActivateExtensionsHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Extensions;
using Razor.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

/// <summary>Handler for activating extensions.</summary>
internal sealed class ActivateExtensionsHandler : CommandHandlerBase
{
    private readonly IExtensionManager _extensionManager;

    /// <summary>Initialises a new instance of the <see cref="ActivateExtensionsHandler"/> class.</summary>
    public ActivateExtensionsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, IExtensionManager extensionManager, ILogger<ActivateExtensionsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _extensionManager = extensionManager;
    }

    public override int CommandId => CommandIds.ActivateExtensions;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Invalid parameters: expected dictionary.", cancellationToken).ConfigureAwait(false);
            return;
        }

        // Extract required fields
        if (!dict.TryGetValue("Adapter", out object? adapterObj) || adapterObj is not string adapterName)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing or invalid 'Adapter' field.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!dict.TryGetValue("Strategy", out object? strategyObj) || strategyObj is not string strategyName)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing or invalid 'Strategy' field.", cancellationToken).ConfigureAwait(false);
            return;
        }

        // Optional fields
        string? nnName = null;
        if (dict.TryGetValue("NeuralNetwork", out object? nnObj) && nnObj is string nnStr)
        {
            nnName = nnStr;
        }

        string[] hookNames = Array.Empty<string>();
        if (dict.TryGetValue("Hooks", out object? hooksObj) && hooksObj is object[] hooksArray)
        {
            hookNames = hooksArray.Select(h => h.ToString()!).ToArray();
        }

        try
        {
            await _extensionManager.ActivateExtensionsAsync(adapterName, strategyName, nnName, hookNames, cancellationToken).ConfigureAwait(false);
            await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Message = "Extensions activated successfully." }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, $"Activation failed: {ex.Message}", cancellationToken).ConfigureAwait(false);
        }
    }
}
