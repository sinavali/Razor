using Chronos.Core.Sdk.Hooks;
using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Live trading hook registration points.
/// </summary>
public sealed class LiveHooks : ILiveHooks
{
    /// <inheritdoc/>
    public IActionRegistration OnStart { get; } = new ActionRegistration();

    /// <inheritdoc/>
    public IFilterRegistration<Tick> OnTickReceived { get; } = new FilterRegistration<Tick>();

    /// <inheritdoc/>
    public IActionRegistration<Tick> OnTickProcessed { get; } = new ActionRegistration<Tick>();

    /// <inheritdoc/>
    public IFilterRegistration<AdapterOrderRequest> OnOrderValidation { get; } = new FilterRegistration<AdapterOrderRequest>();

    /// <inheritdoc/>
    public IFilterRegistration<AdapterOrderRequest> OnOrderBeforeSend { get; } = new FilterRegistration<AdapterOrderRequest>();

    /// <inheritdoc/>
    public IActionRegistration<ExecutionReport> OnOrderExecuted { get; } = new ActionRegistration<ExecutionReport>();

    /// <inheritdoc/>
    public IActionRegistration<(AdapterOrderRequest Request, string Reason)> OnOrderRejected { get; }
        = new ActionRegistration<(AdapterOrderRequest, string)>();

    /// <inheritdoc/>
    public IActionRegistration<Position> OnPositionOpened { get; } = new ActionRegistration<Position>();

    /// <inheritdoc/>
    public IActionRegistration<Position> OnPositionClosed { get; } = new ActionRegistration<Position>();

    /// <inheritdoc/>
    public IActionRegistration<Position> OnPositionStopout { get; } = new ActionRegistration<Position>();

    /// <inheritdoc/>
    public IActionRegistration OnSyncBefore { get; } = new ActionRegistration();

    /// <inheritdoc/>
    public IActionRegistration OnSyncAfter { get; } = new ActionRegistration();

    /// <inheritdoc/>
    public IActionRegistration<int> OnReconnectAttempt { get; } = new ActionRegistration<int>();

    /// <inheritdoc/>
    public IActionRegistration<int> OnReconnectSuccess { get; } = new ActionRegistration<int>();

    /// <inheritdoc/>
    public IActionRegistration OnStop { get; } = new ActionRegistration();

    /// <inheritdoc/>
    public IActionRegistration<EquitySnapshot> OnEquityChanged { get; } = new ActionRegistration<EquitySnapshot>();

    /// <summary>
    /// Clears all registered callbacks from all hook points.
    /// </summary>
    public void ClearAll()
    {
        ((ActionRegistration)OnStart).Clear();
        ((FilterRegistration<Tick>)OnTickReceived).Clear();
        ((ActionRegistration<Tick>)OnTickProcessed).Clear();
        ((FilterRegistration<AdapterOrderRequest>)OnOrderValidation).Clear();
        ((FilterRegistration<AdapterOrderRequest>)OnOrderBeforeSend).Clear();
        ((ActionRegistration<ExecutionReport>)OnOrderExecuted).Clear();
        ((ActionRegistration<(AdapterOrderRequest, string)>)OnOrderRejected).Clear();
        ((ActionRegistration<Position>)OnPositionOpened).Clear();
        ((ActionRegistration<Position>)OnPositionClosed).Clear();
        ((ActionRegistration<Position>)OnPositionStopout).Clear();
        ((ActionRegistration)OnSyncBefore).Clear();
        ((ActionRegistration)OnSyncAfter).Clear();
        ((ActionRegistration<int>)OnReconnectAttempt).Clear();
        ((ActionRegistration<int>)OnReconnectSuccess).Clear();
        ((ActionRegistration)OnStop).Clear();
        ((ActionRegistration<EquitySnapshot>)OnEquityChanged).Clear();
    }
}
