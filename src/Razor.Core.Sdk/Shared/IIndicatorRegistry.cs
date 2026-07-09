namespace Razor.Core.Sdk.Shared;

/// <summary>Registry that creates, caches, and manages indicators.</summary>
public interface IIndicatorRegistry
{
    /// <summary>Gets or creates an indicator of type T with the given arguments.</summary>
    T Get<T>(params object[] args) where T : Indicator;

    /// <summary>All active indicators (for debugging / reporting).</summary>
    IReadOnlyList<Indicator> ActiveIndicators { get; }

    /// <summary>
    /// Removes the specified indicator from the registry and releases its resources.
    /// Returns <c>true</c> if the indicator was found and unregistered; otherwise <c>false</c>.
    /// </summary>
    /// <param name="indicator">The indicator to unregister.</param>
    /// <returns><c>true</c> if the indicator was removed; <c>false</c> if it was not found.</returns>
    bool Unregister(Indicator indicator);

    /// <summary>Disposes all indicators.</summary>
    void DisposeAll();
}

/// <summary>
/// Interface indicating that an indicator requires access to the TickWindow for aggregated data.
/// The indicator registry injects this dependency upon creation.
/// </summary>
public interface IWindowAwareIndicator
{
    /// <summary>Injects the tick window feed.</summary>
    void SetWindow(TickWindow window);
}

/// <summary>
/// Optional interface that indicators can implement to receive the registry for cross‑indicator references.
/// </summary>
public interface IRegistryAwareIndicator
{
    /// <summary>Provides the indicator registry.</summary>
    void SetRegistry(IIndicatorRegistry registry);
}
