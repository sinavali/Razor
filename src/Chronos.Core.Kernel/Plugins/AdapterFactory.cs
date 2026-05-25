using System.Reflection;
using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Plugins;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Discovers adapter implementations in a directory and creates instances by name.
/// </summary>
public sealed class AdapterFactory : IAdapterFactory
{
    private readonly Dictionary<string, Type> _adapterTypes;

    /// <summary>
    /// Initialises the factory by scanning assemblies in the specified plugins folder.
    /// </summary>
    /// <param name="pluginsPath">Directory containing adapter DLLs.</param>
    public AdapterFactory(string pluginsPath)
    {
        ArgumentNullException.ThrowIfNull(pluginsPath);
        if (!Directory.Exists(pluginsPath))
            throw new DirectoryNotFoundException($"Plugins directory not found: {pluginsPath}");

        _adapterTypes = LoadAdapters(pluginsPath);
    }

    /// <inheritdoc/>
    public IAdapter Create(string adapterName)
    {
        if (!_adapterTypes.TryGetValue(adapterName, out var type))
            throw new AdapterException(adapterName, $"No adapter found with name '{adapterName}'.");

        return (IAdapter)Activator.CreateInstance(type)!;
    }

    private static Dictionary<string, Type> LoadAdapters(string pluginsPath)
    {
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var dlls = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dll in dlls)
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                // Validate SDK version before loading adapters
                var versionAttr = asm.GetCustomAttribute<ChronosSdkVersionAttribute>();
                if (versionAttr != null)
                {
                    // Simple check: major version must match 1
                    if (Version.TryParse(versionAttr.Version, out var ver) && ver.Major != 1)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            $"Skipping assembly '{dll}' because it targets SDK version {versionAttr.Version}, but this host requires 1.x.");
                        continue;
                    }
                }

                foreach (var type in asm.GetExportedTypes()
                             .Where(t => t.IsClass && !t.IsAbstract && typeof(IAdapter).IsAssignableFrom(t)))
                {
                    var nameAttr = type.GetCustomAttribute<AdapterNameAttribute>();
                    if (nameAttr != null)
                    {
                        result[nameAttr.Name] = type;
                    }
                }
            }
#pragma warning disable CA1031 // Reason: Plugin loading must gracefully skip any assembly that cannot be loaded.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to load adapter assembly {dll}: {ex.Message}");
            }
        }

        return result;
    }
}
