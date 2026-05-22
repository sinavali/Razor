namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Optional interface that indicators can implement to receive the registry for cross‑indicator references.
/// </summary>
public interface IRegistryAwareIndicator
{
    /// <summary>Provides the indicator registry.</summary>
    void SetRegistry(IIndicatorRegistry registry);
}