#pragma warning disable CA1031 // We are safely wrapping plugin loading failures so entire process won't crash

using System.Reflection;
using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Plugins;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Discovers adapter implementations in a directory and creates instances by name.
/// Implements isolated AssemblyLoadContext loading per Principle 14.
/// </summary>
public sealed class AdapterFactory : IAdapterFactory
{
    private readonly Dictionary<string, Type> _adapterTypes;

    /// <summary>Initializes a new factory by querying available adapters.</summary>
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
                // ARCH-01 Fix: Use isolated context loading.
                var context = new PluginLoadContext(dll);
                var asm = context.LoadFromAssemblyPath(dll);

                // ARCH-02 / BUG-03 Fix: Defer to central validation authority, don't silent load rogue assemblies.
                var validationErrors = PluginValidator.ValidateAssembly(asm, expectedMajor: 1);
                if (validationErrors.Count > 0)
                {
                    foreach (var error in validationErrors)
                        System.Diagnostics.Trace.TraceWarning($"Skipping adapter assembly '{dll}': {error}");
                    continue;
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
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to load adapter assembly {dll}: {ex.Message}");
            }
        }

        return result;
    }
}
#pragma warning restore CA1031
