namespace Chronos.Core.Abstractions.Strategies;

/// <summary>Registry that creates, caches, and manages indicators.</summary>
public interface IIndicatorRegistry
{
    /// <summary>Gets or creates an indicator of type T with the given arguments.</summary>
    T Get<T>(params object[] args) where T : Indicator;
    /// <summary>All active indicators (for debugging / reporting).</summary>
    IReadOnlyList<Indicator> ActiveIndicators { get; }
    /// <summary>
    ///
    /// </summary>
    /// <param name="indicator"></param>
    /// <returns></returns>
    bool Unregister(Indicator indicator);
    /// <summary>Disposes all indicators.</summary>
    void DisposeAll();
}
