using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Kernel.Indicators;

/// <summary>Factory for <see cref="IIndicatorRegistry"/>.</summary>
public static class IndicatorRegistryFactory
{
    /// <summary>Creates a new registry.</summary>
    public static IIndicatorRegistry Create(TickWindow tickWindow) => new IndicatorRegistry(tickWindow);
}
