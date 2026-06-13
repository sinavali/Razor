using System.Reflection;
using System.Runtime.Loader;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// ARCH‑01 Implementation: Provides isolated ALC loading for third‑party Chronos Plugins.
/// Enables version collision avoidance and runtime hot‑unloading capabilities.
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
        if (string.Equals(assemblyName.Name, "Chronos.Core.Abstractions", StringComparison.OrdinalIgnoreCase))
        {
            return null; // Force fallback to default context to share types
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}
