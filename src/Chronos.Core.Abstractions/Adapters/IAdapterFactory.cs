namespace Chronos.Core.Abstractions.Adapters;

/// <summary>
/// Factory that creates adapter instances by name.
/// Hosts implement this to enable adapter pluggability.
/// </summary>
public interface IAdapterFactory
{
    /// <summary>
    /// Creates an adapter instance for the given name.
    /// </summary>
    /// <param name="adapterName">The name of the adapter (e.g., "Nobitex").</param>
    /// <returns>The adapter instance, or throws if the name is unknown.</returns>
    IAdapter Create(string adapterName);
}
