using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Discover adapter implementations in a directory and creates instances by name.
/// </summary>
public sealed class AdapterFactory : IAdapterFactory
{
    private readonly PluginRegistry<IAdapter> _registry;

    /// <summary>Initializes a new factory by querying available adapters.</summary>
    public AdapterFactory(string pluginsPath)
    {
        _registry = new PluginRegistry<IAdapter>(pluginsPath, typeof(AdapterNameAttribute));
    }

    /// <inheritdoc/>
    public IAdapter Create(string adapterName)
    {
        try
        {
            return _registry.Create(adapterName);
        }
        catch (InvalidOperationException)
        {
            throw new AdapterException(adapterName, $"No adapter found with name '{adapterName}'.");
        }
    }
}
