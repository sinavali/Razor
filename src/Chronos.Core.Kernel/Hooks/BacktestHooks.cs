using Chronos.Core.Sdk.Hooks;
using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Backtest hook registration points.
/// </summary>
public sealed class BacktestHooks : IBacktestHooks
{
    /// <inheritdoc/>
    public IFilterRegistration<Tick> OnTickReceived { get; } = new FilterRegistration<Tick>();

    /// <inheritdoc/>
    public IFilterRegistration<Tick> OnTickStrategyBefore { get; } = new FilterRegistration<Tick>();

    /// <inheritdoc/>
    public IFilterRegistration<AdapterOrderRequest> OnOrderValidation { get; } = new FilterRegistration<AdapterOrderRequest>();

    /// <inheritdoc/>
    public IFilterRegistration<AdapterOrderRequest> OnOrderBeforeExecute { get; } = new FilterRegistration<AdapterOrderRequest>();

    /// <inheritdoc/>
    public IActionRegistration<Tick> OnTickStrategyAfter { get; } = new ActionRegistration<Tick>();

    /// <inheritdoc/>
    public IActionRegistration<Tick> OnTickCompleted { get; } = new ActionRegistration<Tick>();

    /// <inheritdoc/>
    public IActionRegistration<(AdapterOrderRequest Request, AdapterOrderResponse Response)> OnOrderAfterExecute { get; }
        = new ActionRegistration<(AdapterOrderRequest, AdapterOrderResponse)>();

    /// <inheritdoc/>
    public IActionRegistration<Position> OnPositionOpened { get; } = new ActionRegistration<Position>();

    /// <inheritdoc/>
    public IActionRegistration<Position> OnPositionClosed { get; } = new ActionRegistration<Position>();

    /// <inheritdoc/>
    public IActionRegistration<Position> OnPositionStopout { get; } = new ActionRegistration<Position>();

    /// <inheritdoc/>
    public IActionRegistration<EquitySnapshot> OnEquityUpdated { get; } = new ActionRegistration<EquitySnapshot>();

    /// <inheritdoc/>
    public IActionRegistration OnStart { get; } = new ActionRegistration();

    /// <inheritdoc/>
    public IActionRegistration OnCompleted { get; } = new ActionRegistration();

    /// <summary>
    /// Clears all registered callbacks from all hook points.
    /// </summary>
    public void ClearAll()
    {
        ((FilterRegistration<Tick>)OnTickReceived).Clear();
        ((FilterRegistration<Tick>)OnTickStrategyBefore).Clear();
        ((FilterRegistration<AdapterOrderRequest>)OnOrderValidation).Clear();
        ((FilterRegistration<AdapterOrderRequest>)OnOrderBeforeExecute).Clear();
        ((ActionRegistration<Tick>)OnTickStrategyAfter).Clear();
        ((ActionRegistration<Tick>)OnTickCompleted).Clear();
        ((ActionRegistration<(AdapterOrderRequest, AdapterOrderResponse)>)OnOrderAfterExecute).Clear();
        ((ActionRegistration<Position>)OnPositionOpened).Clear();
        ((ActionRegistration<Position>)OnPositionClosed).Clear();
        ((ActionRegistration<Position>)OnPositionStopout).Clear();
        ((ActionRegistration<EquitySnapshot>)OnEquityUpdated).Clear();
        ((ActionRegistration)OnStart).Clear();
        ((ActionRegistration)OnCompleted).Clear();
    }
}
