using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Strategies;

/// <summary>
/// Interface indicating that an indicator requires access to the TickWindow for aggregated data.
/// The indicator registry injects this dependency upon creation.
/// </summary>
public interface IWindowAwareIndicator
{
    /// <summary>Injects the tick window feed.</summary>
    void SetWindow(TickWindow window);
}
