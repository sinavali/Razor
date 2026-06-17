using System.Reflection;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Generic registry that discovers and loads plugin implementations via ALC isolation.
/// </summary>
public sealed class PluginRegistry<TPlugin> : IDisposable where TPlugin : class
{
    private readonly Dictionary<string, Type> _pluginTypes;
    private readonly string _pluginTypeIdentifier;
    private readonly List<PluginLoadContext> _contexts = [];
    private bool _disposed;

    /// <summary>List of available discovered plugin names.</summary>
    public IReadOnlyList<string> AvailableNames => _pluginTypes.Keys.ToList();

    /// <summary>Creates a new registry by scanning the given directory for plugins.</summary>
    /// <param name="pluginsPath">The directory containing plugin DLLs.</param>
    /// <param name="attributeType">The discovery attribute type that marks plugin classes (e.g., <c>typeof(AdapterNameAttribute)</c>).</param>
    public PluginRegistry(string pluginsPath, Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(pluginsPath);
        ArgumentNullException.ThrowIfNull(attributeType);

        _pluginTypeIdentifier = typeof(TPlugin).Name;

        if (!Directory.Exists(pluginsPath))
        {
            throw new DirectoryNotFoundException($"Plugins directory not found: {pluginsPath}");
        }

        _pluginTypes = LoadPlugins(pluginsPath, attributeType);
    }

    /// <summary>Creates an instance of the plugin with the given name.</summary>
    /// <param name="name">The plugin name (as declared by its discovery attribute).</param>
    /// <returns>The plugin instance.</returns>
    /// <exception cref="InvalidOperationException">No plugin with the specified name was found, or the plugin does not have a public parameterless constructor.</exception>
    public TPlugin Create(string name)
    {
        if (!_pluginTypes.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"No {_pluginTypeIdentifier} found with name '{name}'.");
        }

        // K‑P1‑6: enforce parameterless constructor
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException(
                $"Plugin type '{type.FullName}' does not have a public parameterless constructor, which is required.");
        }

        return (TPlugin)Activator.CreateInstance(type)!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var context in _contexts)
        {
            try
            {
                context.UnloadContext();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Best effort; unloading may fail if assemblies are still in use.
            }
        }

        _contexts.Clear();
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
                _contexts.Add(context);
                var asm = context.LoadFromAssemblyPath(dll);

                var validationErrors = PluginValidator.ValidateAssembly(asm, expectedMajor: 1);
                if (validationErrors.Count > 0)
                {
                    foreach (var error in validationErrors)
                    {
                        System.Diagnostics.Trace.TraceWarning($"Skipping assembly '{dll}': {error}");
                    }

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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to load assembly {dll}: {ex.Message}");
            }
        }

        return result;
    }
}
