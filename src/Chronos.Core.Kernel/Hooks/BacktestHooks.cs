using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;

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
}
