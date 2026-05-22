using Chronos.Abstractions.Strategies;

namespace Chronos.Kernel.Indicators;

/// <summary>Factory for <see cref="IIndicatorRegistry"/>.</summary>
public static class IndicatorRegistryFactory
{
    /// <summary>Creates a new registry.</summary>
    public static IIndicatorRegistry Create() => new IndicatorRegistry();
}