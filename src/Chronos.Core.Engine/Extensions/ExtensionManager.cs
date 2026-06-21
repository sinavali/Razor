using Chronos.Core.Engine.Core;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Extensions;

internal interface IExtensionManager
{
    Task DiscoverExtensionsAsync(CancellationToken cancellationToken);
    Task ReloadExtensionsAsync(CancellationToken cancellationToken);
    Task ActivateExtensionsAsync(string[] names, CancellationToken cancellationToken);
    Task DeployExtensionAsync(string name, byte[] binaryData, CancellationToken cancellationToken);
    Task RemoveExtensionAsync(string name, CancellationToken cancellationToken);
    Task<object> GetManifestAsync(CancellationToken cancellationToken);
}

internal sealed class ExtensionManager : IExtensionManager, IDisposable
{
    private readonly ILogger<ExtensionManager> _logger;
    private readonly IStateManager _stateManager;
    private readonly string _basePath;
    private readonly ILoggerFactory _loggerFactory;
    private ExtensionCatalog? _catalog;
    private readonly List<string> _loadedExtensions = new();
    private readonly List<string> _activeExtensions = new();
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logDiscoveringExtensions =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Discovering extensions in {BasePath}...");
    private static readonly Action<ILogger, int, int, Exception?> _logDiscoveryResult =
        LoggerMessage.Define<int, int>(LogLevel.Information, 1, "Discovered {AdapterCount} adapters, {StrategyCount} strategies.");
    private static readonly Action<ILogger, string, Exception?> _logActivatingExtensions =
        LoggerMessage.Define<string>(LogLevel.Information, 2, "Activating extensions: {Names}");
    private static readonly Action<ILogger, string, Exception?> _logExtensionNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, 3, "Extension '{Name}' not found in catalog and cannot be activated.");
    private static readonly Action<ILogger, string, Exception?> _logExtensionActivated =
        LoggerMessage.Define<string>(LogLevel.Information, 4, "Successfully activated extension: {Name}");
    private static readonly Action<ILogger, string, int, Exception?> _logDeployingExtension =
        LoggerMessage.Define<string, int>(LogLevel.Information, 5, "Deploying extension {Name} ({Size} bytes).");
    private static readonly Action<ILogger, string, Exception?> _logExtensionDeployed =
        LoggerMessage.Define<string>(LogLevel.Information, 6, "Extension {Name} successfully deployed to disk.");
    private static readonly Action<ILogger, string, Exception?> _logRemovingExtension =
        LoggerMessage.Define<string>(LogLevel.Information, 7, "Removing extension {Name}.");
    private static readonly Action<ILogger, string, Exception?> _logExtensionFileDeleted =
        LoggerMessage.Define<string>(LogLevel.Information, 8, "Extension file {File} deleted.");
    private static readonly Action<ILogger, string, Exception?> _logExtensionFileNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, 9, "Extension file {File} does not exist.");

    private static readonly Action<ILogger, Exception?> _logExtensionsReloaded =
        LoggerMessage.Define(LogLevel.Information, 10, "Extensions reloaded successfully.");

    public ExtensionManager(ILogger<ExtensionManager> logger, IStateManager stateManager, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _stateManager = stateManager;
        _loggerFactory = loggerFactory;
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    public async Task DiscoverExtensionsAsync(CancellationToken cancellationToken)
    {
        _logDiscoveringExtensions(_logger, _basePath, null);
        await Task.Run(() =>
        {
            _catalog?.Dispose();
            var catalogLogger = _loggerFactory.CreateLogger<ExtensionCatalog>();
            _catalog = new ExtensionCatalog(_basePath, catalogLogger);

            _loadedExtensions.Clear();
            _loadedExtensions.AddRange(_catalog.AdapterNames);
            _loadedExtensions.AddRange(_catalog.StrategyNames);
            _loadedExtensions.AddRange(_catalog.IndicatorNames);
            _loadedExtensions.AddRange(_catalog.NeuralNetworkNames);

            _logDiscoveryResult(_logger, _catalog.AdapterNames.Count, _catalog.StrategyNames.Count, null);
        }, cancellationToken).ConfigureAwait(false);

        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false);
        await _stateManager.SaveExtensionManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReloadExtensionsAsync(CancellationToken cancellationToken)
    {
        _catalog?.Dispose();
        _catalog = null;
        _loadedExtensions.Clear();
        _activeExtensions.Clear();

        await DiscoverExtensionsAsync(cancellationToken).ConfigureAwait(false);
        _logExtensionsReloaded(_logger, null);
    }

    public async Task ActivateExtensionsAsync(string[] names, CancellationToken cancellationToken)
    {
        _logActivatingExtensions(_logger, string.Join(", ", names), null);
        if (_catalog == null)
        {
            throw new InvalidOperationException("Catalog not initialized. Discovery must run first.");
        }

        await Task.Run(() =>
        {
            _activeExtensions.Clear();
            foreach (var name in names)
            {
                if (!_loadedExtensions.Contains(name))
                {
                    _logExtensionNotFound(_logger, name, null);
                    continue;
                }
                _activeExtensions.Add(name);
                _logExtensionActivated(_logger, name, null);
            }
        }, cancellationToken).ConfigureAwait(false);

        await _stateManager.SaveExtensionManifestAsync(new { ActivatedExtensions = _activeExtensions.ToArray() }, cancellationToken).ConfigureAwait(false);
    }

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

    public Task<object> GetManifestAsync(CancellationToken cancellationToken)
    {
        var manifest = new
        {
            Adapters = _catalog?.AdapterNames ?? Array.Empty<string>(),
            Strategies = _catalog?.StrategyNames ?? Array.Empty<string>(),
            Indicators = _catalog?.IndicatorNames ?? Array.Empty<string>(),
            NeuralNetworks = _catalog?.NeuralNetworkNames ?? Array.Empty<string>(),
            HookPlugins = _catalog?.HookManifests.Select(h => h.GetType().FullName).ToArray() ?? Array.Empty<string>(),
            ActiveExtensions = _activeExtensions.ToArray()
        };
        return Task.FromResult<object>(manifest);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog?.Dispose();
    }
}
