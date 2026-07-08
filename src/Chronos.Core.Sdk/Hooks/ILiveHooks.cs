using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.Hooks;

/// <summary>Hook registration points for the live trading pipeline.</summary>
public interface ILiveHooks
{
    /// <summary>Fires when a live trading session starts.</summary>
    IActionRegistration OnStart { get; }

    /// <summary>Filter a tick as it is received from the adapter.</summary>
    IFilterRegistration<Tick> OnTickReceived { get; }

    /// <summary>Action after a tick has been fully processed.</summary>
    IActionRegistration<Tick> OnTickProcessed { get; }

    /// <summary>Filter an order request before it is sent to the exchange.</summary>
    IFilterRegistration<AdapterOrderRequest> OnOrderValidation { get; }

    /// <summary>Filter an order request immediately before sending.</summary>
    IFilterRegistration<AdapterOrderRequest> OnOrderBeforeSend { get; }

    /// <summary>Action when an execution report is received.</summary>
    IActionRegistration<ExecutionReport> OnOrderExecuted { get; }

    /// <summary>Action when an order is rejected by the exchange.</summary>
    IActionRegistration<(AdapterOrderRequest Request, string Reason)> OnOrderRejected { get; }

    /// <summary>Action when a new position is detected.</summary>
    IActionRegistration<Position> OnPositionOpened { get; }

    /// <summary>Action when a position is closed.</summary>
    IActionRegistration<Position> OnPositionClosed { get; }

    /// <summary>Action when a stop‑out occurs.</summary>
    IActionRegistration<Position> OnPositionStopout { get; }

    /// <summary>Action before periodic state sync.</summary>
    IActionRegistration OnSyncBefore { get; }

    /// <summary>Action after periodic state sync.</summary>
    IActionRegistration OnSyncAfter { get; }

    /// <summary>Action on a reconnection attempt.</summary>
    IActionRegistration<int> OnReconnectAttempt { get; }

    /// <summary>Action on a successful reconnection.</summary>
    IActionRegistration<int> OnReconnectSuccess { get; }

    /// <summary>Fires when the live session stops.</summary>
    IActionRegistration OnStop { get; }

    /// <summary>Action when equity changes.</summary>
    IActionRegistration<EquitySnapshot> OnEquityChanged { get; }
}
