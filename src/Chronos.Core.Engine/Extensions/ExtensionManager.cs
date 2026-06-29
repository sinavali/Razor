using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Chronos.Core.Engine.Core;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Extensions;

/// <summary>Default implementation of <see cref="IExtensionManager"/>.</summary>
internal sealed class ExtensionManager : IExtensionManager, IDisposable
{
    private readonly ILogger<ExtensionManager> _logger;
    private readonly IStateManager _stateManager;
    private readonly string _basePath;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHookRegistry _hookRegistry;
    private readonly IBehaviorRecorder _behaviorRecorder;
    private ExtensionCatalog? _catalog;
    private readonly List<string> _activeExtensionNames = new();
    private bool _disposed;

    // Active instances
    private IAdapterCapability? _activeAdapter;
    private IStrategyCapability? _activeStrategy;
    private INeuralNetworkModel? _activeNeuralNetwork;
    private readonly List<IHookManifest> _activeHookManifests = new();

    private static readonly Action<ILogger, string, Exception?> _logDiscoveringExtensions =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Discovering extensions in {BasePath}...");
    private static readonly Action<ILogger, int, int, Exception?> _logDiscoveryResult =
        LoggerMessage.Define<int, int>(LogLevel.Information, 1, "Discovered {AdapterCount} adapters, {StrategyCount} strategies.");
    private static readonly Action<ILogger, string, string, string, string, Exception?> _logActivatingExtensions =
        LoggerMessage.Define<string, string, string, string>(LogLevel.Information, 2, "Activating extensions: Adapter={Adapter}, Strategy={Strategy}, NN={NN}, Hooks={Hooks}");
    private static readonly Action<ILogger, string, Exception?> _logExtensionNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, 3, "Extension '{Name}' not found in catalog and cannot be activated.");
    private static readonly Action<ILogger, string, Exception?> _logExtensionActivated =
        LoggerMessage.Define<string>(LogLevel.Information, 4, "Successfully activated extension: {Name}");
    private static readonly Action<ILogger, string, int, Exception?> _logDeployingExtension =
        LoggerMessage.Define<string, int>(LogLevel.Information, 6, "Deploying extension {Name} ({Size} bytes).");
    private static readonly Action<ILogger, string, Exception?> _logExtensionDeployed =
        LoggerMessage.Define<string>(LogLevel.Information, 7, "Extension {Name} successfully deployed to disk.");
    private static readonly Action<ILogger, string, Exception?> _logRemovingExtension =
        LoggerMessage.Define<string>(LogLevel.Information, 8, "Removing extension {Name}.");
    private static readonly Action<ILogger, string, Exception?> _logExtensionFileDeleted =
        LoggerMessage.Define<string>(LogLevel.Information, 9, "Extension file {File} deleted.");
    private static readonly Action<ILogger, string, Exception?> _logExtensionFileNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, 10, "Extension file {File} does not exist.");
    private static readonly Action<ILogger, Exception?> _logExtensionsReloaded =
        LoggerMessage.Define(LogLevel.Information, 11, "Extensions reloaded successfully.");
    private static readonly Action<ILogger, string, Exception?> _logStrategyDoesNotExposeSpec =
        LoggerMessage.Define<string>(LogLevel.Warning, 12, "Strategy {StrategyName} does not expose StrategySpecification; cannot validate symbol/timeframe support.");
    private static readonly Action<ILogger, string, Exception?> _logUnusedNeuralNetwork =
        LoggerMessage.Define<string>(LogLevel.Warning, 13, "Strategy {StrategyName} does not require a neural network, but one was provided. It will be ignored.");

    // Behavior recording hook – registered internally by the engine.
    private InternalBehaviorHook? _behaviorHook;

    public IAdapterCapability? ActiveAdapter => _activeAdapter;
    public IStrategyCapability? ActiveStrategy => _activeStrategy;
    public INeuralNetworkModel? ActiveNeuralNetwork => _activeNeuralNetwork;
    public IHookRegistry HookRegistry => _hookRegistry;

    public ExtensionManager(
        ILogger<ExtensionManager> logger,
        IStateManager stateManager,
        ILoggerFactory loggerFactory,
        IHookRegistry hookRegistry,
        IBehaviorRecorder behaviorRecorder)
    {
        _logger = logger;
        _stateManager = stateManager;
        _loggerFactory = loggerFactory;
        _hookRegistry = hookRegistry ?? throw new ArgumentNullException(nameof(hookRegistry));
        _behaviorRecorder = behaviorRecorder ?? throw new ArgumentNullException(nameof(behaviorRecorder));
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <inheritdoc/>
    public async Task DiscoverExtensionsAsync(CancellationToken cancellationToken)
    {
        _logDiscoveringExtensions(_logger, _basePath, null);
        await Task.Run(() =>
        {
            _catalog?.Dispose();
            var catalogLogger = _loggerFactory.CreateLogger<ExtensionCatalog>();
            _catalog = new ExtensionCatalog(_basePath, catalogLogger);

            _logDiscoveryResult(_logger, _catalog.AdapterNames.Count, _catalog.StrategyNames.Count, null);
        }, cancellationToken).ConfigureAwait(false);

        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false);
        await _stateManager.SaveExtensionManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ReloadExtensionsAsync(CancellationToken cancellationToken)
    {
        _catalog?.Dispose();
        _catalog = null;
        _activeExtensionNames.Clear();
        _activeAdapter = null;
        _activeStrategy = null;
        _activeNeuralNetwork = null;
        _activeHookManifests.Clear();

        await DiscoverExtensionsAsync(cancellationToken).ConfigureAwait(false);
        _logExtensionsReloaded(_logger, null);
    }

    /// <inheritdoc/>
    public async Task ActivateExtensionsAsync(string adapterName, string strategyName, string? nnModelName, string[] hookPluginNames, CancellationToken cancellationToken)
    {
        if (_catalog == null)
        {
            throw new InvalidOperationException("Catalog not initialized. Discovery must run first.");
        }

        _logActivatingExtensions(_logger, adapterName, strategyName, nnModelName ?? "none", string.Join(",", hookPluginNames), null);

        // 1. Instantiate adapter
        if (!_catalog.AdapterNames.Contains(adapterName))
        {
            throw new InvalidOperationException($"Adapter '{adapterName}' not found.");
        }
        _activeAdapter = _catalog.CreateAdapter(adapterName);
        _logExtensionActivated(_logger, $"Adapter: {adapterName}", null);

        // 2. Instantiate strategy
        if (!_catalog.StrategyNames.Contains(strategyName))
        {
            throw new InvalidOperationException($"Strategy '{strategyName}' not found.");
        }
        _activeStrategy = _catalog.CreateStrategy(strategyName);
        _logExtensionActivated(_logger, $"Strategy: {strategyName}", null);

        // 3. Instantiate neural network if provided
        if (!string.IsNullOrEmpty(nnModelName))
        {
            if (!_catalog.NeuralNetworkNames.Contains(nnModelName))
            {
                throw new InvalidOperationException($"Neural network model '{nnModelName}' not found.");
            }
            _activeNeuralNetwork = _catalog.CreateNeuralNetworkModel(nnModelName);
            _logExtensionActivated(_logger, $"NN Model: {nnModelName}", null);

            if (_activeStrategy is IStrategyCapability strategy)
            {
                strategy.NeuralNetwork = _activeNeuralNetwork;
            }
        }
        else
        {
            _activeNeuralNetwork = null;
        }

        // 4. Activate hook plugins
        _activeHookManifests.Clear();
        foreach (var hookName in hookPluginNames)
        {
            var manifest = _catalog.HookManifests.FirstOrDefault(h => h.GetType().FullName == hookName || h.GetType().Name == hookName);
            if (manifest == null)
            {
                _logExtensionNotFound(_logger, hookName, null);
                continue;
            }
            _activeHookManifests.Add(manifest);
            manifest.RegisterHooks(_hookRegistry);
            _logExtensionActivated(_logger, $"Hook: {hookName}", null);
        }

        // 5. Validate the active set
        ValidateActiveSet();

        // 6. Save the active set to state
        _activeExtensionNames.Clear();
        _activeExtensionNames.Add(adapterName);
        _activeExtensionNames.Add(strategyName);
        if (!string.IsNullOrEmpty(nnModelName))
        {
            _activeExtensionNames.Add(nnModelName);
        }

        _activeExtensionNames.AddRange(hookPluginNames);

        await _stateManager.SaveExtensionManifestAsync(new
        {
            Adapter = adapterName,
            Strategy = strategyName,
            NeuralNetwork = nnModelName,
            Hooks = hookPluginNames
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ValidateActiveSet()
    {
        if (_activeStrategy == null || _activeAdapter == null)
        {
            throw new InvalidOperationException("Both adapter and strategy must be active.");
        }

        if (_activeStrategy.RequiresNeuralNetwork && _activeNeuralNetwork == null)
        {
            throw new InvalidOperationException("Strategy requires a neural network but none was provided.");
        }
        if (!_activeStrategy.RequiresNeuralNetwork && _activeNeuralNetwork != null)
        {
            _logUnusedNeuralNetwork(_logger, _activeStrategy.GetType().Name, null);
        }

        var spec = _activeStrategy.GetType().GetProperty("Spec")?.GetValue(_activeStrategy) as StrategySpecification;
        if (spec == null)
        {
            _logStrategyDoesNotExposeSpec(_logger, _activeStrategy.GetType().Name, null);
            return;
        }

        foreach (var symbolRequest in spec.RequestedSymbols)
        {
            var supported = _activeAdapter.GetSupportedTimeframes(symbolRequest.Symbol);
            if (supported != null)
            {
                foreach (var tf in symbolRequest.TimeFrames)
                {
                    if (!supported.Contains(tf))
                    {
                        throw new InvalidOperationException(
                            $"Adapter does not support timeframe {tf} for symbol {symbolRequest.Symbol}.");
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task DeployExtensionAsync(string name, byte[] binaryData, CancellationToken cancellationToken)
    {
        _logDeployingExtension(_logger, name, binaryData.Length, null);
        if (string.IsNullOrWhiteSpace(name) || binaryData == null || binaryData.Length == 0)
        {
            throw new ArgumentException("Invalid extension name or binary data.");
        }

        await Task.Run(() =>
        {
            var extensionPath = Path.Combine(_basePath, "Plugins", $"{name}.dll");
            var directory = Path.GetDirectoryName(extensionPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(extensionPath, binaryData);
        }, cancellationToken).ConfigureAwait(false);

        _logExtensionDeployed(_logger, name, null);
    }

    /// <inheritdoc/>
    public async Task RemoveExtensionAsync(string name, CancellationToken cancellationToken)
    {
        _logRemovingExtension(_logger, name, null);
        await Task.Run(() =>
        {
            var extensionPath = Path.Combine(_basePath, "Plugins", $"{name}.dll");
            if (File.Exists(extensionPath))
            {
                File.Delete(extensionPath);
                _logExtensionFileDeleted(_logger, extensionPath, null);
            }
            else
            {
                _logExtensionFileNotFound(_logger, extensionPath, null);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<object> GetManifestAsync(CancellationToken cancellationToken)
    {
        var manifest = new
        {
            Adapters = _catalog?.AdapterNames ?? Array.Empty<string>(),
            Strategies = _catalog?.StrategyNames ?? Array.Empty<string>(),
            Indicators = _catalog?.IndicatorNames ?? Array.Empty<string>(),
            NeuralNetworks = _catalog?.NeuralNetworkNames ?? Array.Empty<string>(),
            HookPlugins = _catalog?.HookManifests.Select(h => h.GetType().FullName).ToArray() ?? Array.Empty<string>(),
            ActiveExtensions = _activeExtensionNames.ToArray()
        };
        return Task.FromResult<object>(manifest);
    }

    /// <inheritdoc/>
    public IStrategyCapability? CreateTransientStrategy(string strategyName)
    {
        if (_catalog == null)
        {
            throw new InvalidOperationException("Catalog not initialized.");
        }

        if (!_catalog.StrategyNames.Contains(strategyName))
        {
            _logExtensionNotFound(_logger, strategyName, null);
            return null;
        }

        var strategy = _catalog.CreateStrategy(strategyName);

        // If the active strategy had a neural network, we need to propagate it? 
        // But for transient evaluation, we may not need it because we can set it from the caller.
        // The caller can set NeuralNetwork property if needed.
        // We'll leave it unset, and the caller can set it.

        return strategy;
    }

    // ─── Behavior logging support (via internal hook) ──────────

    /// <inheritdoc/>
    public void EnableBehaviorLoggingOnStrategy(string sessionId, int snapshotIntervalSeconds = 10)
    {
        if (_behaviorHook == null)
        {
            _behaviorHook = new InternalBehaviorHook(_behaviorRecorder, snapshotIntervalSeconds);
            _hookRegistry.Backtest.OnTickStrategyAfter.Register(
                (tick, ctx) => _behaviorHook.OnTickAfter(tick, ctx),
                priority: int.MinValue); // run first, before user hooks
        }
        _behaviorHook.Enable(sessionId);
    }

    /// <inheritdoc/>
    public void DisableBehaviorLoggingOnStrategy()
    {
        _behaviorHook?.Disable();
        // The hook remains registered, but it will no‑op while disabled.
    }

    /// <inheritdoc/>
    public void RecordSnapshotOnStrategy()
    {
        _behaviorHook?.RecordSnapshot();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog?.Dispose();
        (_activeAdapter as IDisposable)?.Dispose();
        (_activeStrategy as IDisposable)?.Dispose();
        (_activeNeuralNetwork as IDisposable)?.Dispose();
        foreach (var hook in _activeHookManifests)
        {
            (hook as IDisposable)?.Dispose();
        }
    }

    // ─── Nested internal hook class ─────────────────────────────

    private sealed class InternalBehaviorHook
    {
        private readonly IBehaviorRecorder _recorder;
        private readonly int _snapshotIntervalSeconds;
        private string _sessionId = string.Empty;
        private bool _enabled;
        private long _lastSnapshotTicks;

        public InternalBehaviorHook(IBehaviorRecorder recorder, int snapshotIntervalSeconds)
        {
            _recorder = recorder;
            _snapshotIntervalSeconds = snapshotIntervalSeconds;
        }

        public void Enable(string sessionId)
        {
            _sessionId = sessionId;
            _enabled = true;
            _lastSnapshotTicks = DateTime.UtcNow.Ticks;
        }

        public void Disable()
        {
            _enabled = false;
        }

        public void RecordSnapshot()
        {
            if (!_enabled)
            {
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            long interval = _snapshotIntervalSeconds * TimeSpan.TicksPerSecond;
            if (now - _lastSnapshotTicks < interval)
            {
                return;
            }
            _lastSnapshotTicks = now;

            var record = new BehaviorRecord
            {
                TimestampUtc = DateTime.UtcNow,
                SessionId = _sessionId,
                Action = "Snapshot"
            };
            _recorder.Record(record);
        }

        public void OnTickAfter(Tick tick, IHookContext context)
        {
            // Called after the strategy processes the tick.
            if (_enabled && _recorder.IsEnabled)
            {
                RecordSnapshot();
            }
        }
    }
}
