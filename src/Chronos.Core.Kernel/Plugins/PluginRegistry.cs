using System.Reflection;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Generic registry that discovers and loads plugin implementations via ALC isolation.
/// </summary>
public sealed class PluginRegistry<TPlugin> where TPlugin : class
{
    private readonly Dictionary<string, Type> _pluginTypes;
    private readonly string _pluginTypeIdentifier;

    /// <summary>List of available discovered plugin names.</summary>
    public IReadOnlyList<string> AvailableNames => _pluginTypes.Keys.ToList();

    public PluginRegistry(string pluginsPath, Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(pluginsPath);
        ArgumentNullException.ThrowIfNull(attributeType);

        _pluginTypeIdentifier = typeof(TPlugin).Name;

        if (!Directory.Exists(pluginsPath))
            throw new DirectoryNotFoundException($"Plugins directory not found: {pluginsPath}");

        _pluginTypes = LoadPlugins(pluginsPath, attributeType);
    }

    public TPlugin Create(string name)
    {
        if (!_pluginTypes.TryGetValue(name, out var type))
            throw new InvalidOperationException($"No {_pluginTypeIdentifier} found with name '{name}'.");

        return (TPlugin)Activator.CreateInstance(type)!;
    }

    private Dictionary<string, Type> LoadPlugins(string pluginsPath, Type attributeType)
    {
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var dlls = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dll in dlls)
        {
            try
            {
                var context = new PluginLoadContext(dll);
                var asm = context.LoadFromAssemblyPath(dll);

                var validationErrors = PluginValidator.ValidateAssembly(asm, expectedMajor: 1);
                if (validationErrors.Count > 0)
                {
                    foreach (var error in validationErrors)
                        System.Diagnostics.Trace.TraceWarning($"Skipping assembly '{dll}': {error}");
                    continue;
                }

                foreach (var type in asm.GetExportedTypes()
                             .Where(t => t.IsClass && !t.IsAbstract && typeof(TPlugin).IsAssignableFrom(t)))
                {
                    var attr = type.GetCustomAttribute(attributeType);
                    if (attr != null)
                    {
                        var nameProperty = attributeType.GetProperty("Name");
                        if (nameProperty != null && nameProperty.GetValue(attr) is string nameValue)
                        {
                            result[nameValue] = type;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to load assembly {dll}: {ex.Message}");
            }
        }

        return result;
    }
}
