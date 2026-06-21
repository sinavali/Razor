using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;

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
}
