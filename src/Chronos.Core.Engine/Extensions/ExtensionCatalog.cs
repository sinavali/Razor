using System.Reflection;
using Chronos.Core.Abstractions;
using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Extensions;

/// <summary>
/// Discovers and loads extension assemblies.
/// </summary>
internal sealed class ExtensionCatalog : IDisposable
{
    private readonly string _basePath;
    private readonly List<PluginLoadContext> _contexts = new();
    private readonly Dictionary<string, Type> _adapterTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _strategyTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _indicatorTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _nnModelTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IHookManifest> _hookManifests = new();
    private readonly ILogger<ExtensionCatalog> _logger;
    private bool _disposed;

    // LoggerMessage delegate
    private static readonly Action<ILogger, string, Exception?> _logAdapterInstantiationFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, 0, "Failed to instantiate adapter type {Type} to read its name.");


    /// <param name="basePath"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public ExtensionCatalog(string basePath, ILogger<ExtensionCatalog> logger)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ScanAll();
    }

    public IAdapterCapability CreateAdapter(string name)
    {
        if (!_adapterTypes.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"Adapter '{name}' not found.");
        }

        return (IAdapterCapability)Activator.CreateInstance(type)!;
    }

    public IStrategyCapability CreateStrategy(string name)
    {
        if (!_strategyTypes.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"Strategy '{name}' not found.");
        }

        return (IStrategyCapability)Activator.CreateInstance(type)!;
    }

    public INeuralNetworkModel CreateNeuralNetworkModel(string name)
    {
        if (!_nnModelTypes.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"Neural network model '{name}' not found.");
        }

        return (INeuralNetworkModel)Activator.CreateInstance(type)!;
    }

    public Indicator CreateIndicator(string name)
    {
        if (!_indicatorTypes.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"Indicator '{name}' not found.");
        }

        return (Indicator)Activator.CreateInstance(type)!;
    }

    public IReadOnlyList<IHookManifest> HookManifests => _hookManifests.AsReadOnly();
    public IReadOnlyList<string> AdapterNames => _adapterTypes.Keys.ToList().AsReadOnly();
    public IReadOnlyList<string> StrategyNames => _strategyTypes.Keys.ToList().AsReadOnly();
    public IReadOnlyList<string> IndicatorNames => _indicatorTypes.Keys.ToList().AsReadOnly();
    public IReadOnlyList<string> NeuralNetworkNames => _nnModelTypes.Keys.ToList().AsReadOnly();

    private void ScanAll()
    {
        string[] dirs = { "Adapters", "Strategies", "Indicators", "Plugins", "NeuralNetworks" };
        foreach (var dir in dirs)
        {
            var path = Path.Combine(_basePath, dir);
            if (Directory.Exists(path))
            {
                ScanDirectory(path);
            }
        }
    }

    private void ScanDirectory(string directory)
    {
        ScanDlls(directory);
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            ScanDlls(subDir);
        }
    }

    private void ScanDlls(string directory)
    {
        foreach (var dll in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var context = new PluginLoadContext(dll);
                _contexts.Add(context);
                var asm = context.LoadFromAssemblyPath(dll);

                var errors = PluginValidator.ValidateAssembly(asm);
                if (errors.Any())
                {
                    foreach (var err in errors)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Skipping {dll}: {err}");
                    }

                    continue;
                }

                var safetyErrors = PluginSafetyValidator.Validate(asm);
                if (safetyErrors.Any())
                {
                    foreach (var err in safetyErrors)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Safety validation failed for {dll}: {err}");
                    }

                    continue;
                }

                foreach (var type in asm.GetExportedTypes().Where(t => t.IsClass && !t.IsAbstract))
                {
                    if (typeof(IAdapterCapability).IsAssignableFrom(type))
                    {
                        RegisterAdapter(type);
                    }

                    if (typeof(IStrategyCapability).IsAssignableFrom(type))
                    {
                        _strategyTypes[type.FullName!] = type;
                    }

                    if (typeof(INeuralNetworkModel).IsAssignableFrom(type))
                    {
                        _nnModelTypes[type.FullName!] = type;
                    }

                    if (typeof(Indicator).IsAssignableFrom(type))
                    {
                        _indicatorTypes[type.FullName!] = type;
                    }

                    if (typeof(IHookManifest).IsAssignableFrom(type))
                    {
                        _hookManifests.Add((IHookManifest)Activator.CreateInstance(type)!);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to load {dll}: {ex.Message}");
            }
        }
    }

    private void RegisterAdapter(Type type)
    {
        _adapterTypes[type.FullName!] = type;
        var attr = type.GetCustomAttribute<AdapterNameAttribute>();
        if (attr != null)
        {
            _adapterTypes[attr.Name] = type;
            return;
        }
        try
        {
            var temp = (IAdapterCapability)Activator.CreateInstance(type)!;
            _adapterTypes[temp.Name] = type;
        }
        catch (Exception ex)
        {
            _logAdapterInstantiationFailed(_logger, type.FullName!, ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var context in _contexts)
        {
            try { context.UnloadContext(); }
            catch
            {
                // ignored
            }
        }
        _contexts.Clear();
    }
}
