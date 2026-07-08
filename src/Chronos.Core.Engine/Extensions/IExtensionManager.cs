using Chronos.Core.Sdk.Hooks;
using Chronos.Core.Sdk.Slots.Adapter;
using Chronos.Core.Sdk.Slots.NeuralNetwork;
using Chronos.Core.Sdk.Slots.Strategy;

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

    // ─── Transient instance support ──────────────────────────────

    /// <summary>
    /// Creates a new transient instance of the specified strategy type.
    /// This instance is not tracked by the extension manager and must be disposed by the caller.
    /// Used for multi-threaded evaluation (e.g., genetic algorithm) where each thread needs its own strategy.
    /// </summary>
    IStrategyCapability? CreateTransientStrategy(string strategyName);

    // ─── Behavior logging support ──────────────────────────

    /// <summary>Enables behavior logging on the active strategy.</summary>
    void EnableBehaviorLoggingOnStrategy(string sessionId, int snapshotIntervalSeconds = 10);

    /// <summary>Disables behavior logging on the active strategy.</summary>
    void DisableBehaviorLoggingOnStrategy();

    /// <summary>Records a snapshot on the active strategy if logging is enabled.</summary>
    void RecordSnapshotOnStrategy();
}
