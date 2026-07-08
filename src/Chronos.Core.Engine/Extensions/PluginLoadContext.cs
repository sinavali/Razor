using System.Reflection;
using System.Runtime.Loader;

namespace Chronos.Core.Engine.Extensions;

/// <summary>
/// Isolated assembly load context for plugins.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, "Chronos.Core.Sdk", StringComparison.OrdinalIgnoreCase))
        {
            return null; // Fallback to default context
        }
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }

    public void UnloadContext() => Unload();
}
