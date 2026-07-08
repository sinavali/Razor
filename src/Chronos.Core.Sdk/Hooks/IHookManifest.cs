namespace Chronos.Core.Sdk.Hooks;

/// <summary>
/// Entry point for hook plugin assemblies.
/// The engine calls this once per loaded plugin DLL to allow registration
/// of hook callbacks.
/// </summary>
public interface IHookManifest
{
    /// <summary>
    /// Register hook callbacks with the registry.
    /// Called once at plugin load time. The order of registrations
    /// within this method determines tie‑breaking for same‑priority hooks.
    /// </summary>
    /// <param name="registry">The root hook registry.</param>
    void RegisterHooks(IHookRegistry registry);
}
