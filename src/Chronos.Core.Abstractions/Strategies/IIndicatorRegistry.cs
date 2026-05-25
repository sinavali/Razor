namespace Chronos.Core.Abstractions.Strategies;

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
