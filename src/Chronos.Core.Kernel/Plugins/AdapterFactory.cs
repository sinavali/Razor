using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Discover adapter implementations in a directory and creates instances by name.
/// </summary>
public sealed class AdapterFactory : IAdapterFactory, IDisposable
{
    private readonly PluginRegistry<IAdapter> _registry;
    private bool _disposed;

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
        catch (InvalidOperationException ex)
        {
            throw new AdapterException(adapterName, ex.Message);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registry.Dispose();
    }
}
