using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Kernel.Indicators;

/// <summary>Factory for <see cref="IIndicatorRegistry"/>.</summary>
public static class IndicatorRegistryFactory
{
    /// <summary>Creates a new registry for the given tick window.</summary>
    public static IIndicatorRegistry Create(TickWindow tickWindow) => new IndicatorRegistry(tickWindow);
}
