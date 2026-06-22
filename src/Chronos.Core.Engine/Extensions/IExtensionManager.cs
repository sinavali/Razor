using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Slots;

namespace Chronos.Core.Engine.Extensions;

/// <summary>Manages the discovery, loading, and activation of extensions.</summary>
internal interface IExtensionManager
{
    /// <summary>Discovers all extensions in the designated directories.</summary>
    Task DiscoverExtensionsAsync(CancellationToken cancellationToken);

    /// <summary>Reloads all extensions.</summary>
    Task ReloadExtensionsAsync(CancellationToken cancellationToken);

    /// <summary>Activates the specified extensions.</summary>
    Task ActivateExtensionsAsync(string adapterName, string strategyName, string? nnModelName, string[] hookPluginNames, CancellationToken cancellationToken);

    /// <summary>Deploys a new extension DLL.</summary>
    Task DeployExtensionAsync(string name, byte[] binaryData, CancellationToken cancellationToken);

    /// <summary>Removes an extension by name.</summary>
    Task RemoveExtensionAsync(string name, CancellationToken cancellationToken);

    /// <summary>Gets the current extension manifest.</summary>
    Task<object> GetManifestAsync(CancellationToken cancellationToken);

    /// <summary>Gets the currently active adapter, or null if none.</summary>
    IAdapterCapability? ActiveAdapter { get; }

    /// <summary>Gets the currently active strategy, or null if none.</summary>
    IStrategyCapability? ActiveStrategy { get; }

    /// <summary>Gets the currently active neural network model, or null if none.</summary>
    INeuralNetworkModel? ActiveNeuralNetwork { get; }

    /// <summary>Gets the hook registry containing all registered hook plugins.</summary>
    IHookRegistry HookRegistry { get; }
}
