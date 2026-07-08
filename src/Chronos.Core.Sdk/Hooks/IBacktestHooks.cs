using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.Hooks;

/// <summary>Hook registration points for the backtesting pipeline.</summary>
public interface IBacktestHooks
{
    /// <summary>Fires when a backtest starts.</summary>
    IActionRegistration OnStart { get; }

    /// <summary>Filter a tick as it is received from the tick stream.</summary>
    IFilterRegistration<Tick> OnTickReceived { get; }

    /// <summary>Filter the tick before it is passed to the strategy.</summary>
    IFilterRegistration<Tick> OnTickStrategyBefore { get; }

    /// <summary>Action after the strategy has processed a tick.</summary>
    IActionRegistration<Tick> OnTickStrategyAfter { get; }

    /// <summary>Action after all processing for a tick is complete.</summary>
    IActionRegistration<Tick> OnTickCompleted { get; }

    /// <summary>Filter an order request before any validation occurs.</summary>
    IFilterRegistration<AdapterOrderRequest> OnOrderValidation { get; }

    /// <summary>Filter an order request just before execution.</summary>
    IFilterRegistration<AdapterOrderRequest> OnOrderBeforeExecute { get; }

    /// <summary>Action after an order is executed (or rejected).</summary>
    IActionRegistration<(AdapterOrderRequest Request, AdapterOrderResponse Response)> OnOrderAfterExecute { get; }

    /// <summary>Action when a new position is opened.</summary>
    IActionRegistration<Position> OnPositionOpened { get; }

    /// <summary>Action when a position is closed.</summary>
    IActionRegistration<Position> OnPositionClosed { get; }

    /// <summary>Action when a stop‑out occurs.</summary>
    IActionRegistration<Position> OnPositionStopout { get; }

    /// <summary>Action when equity/drawdown is recalculated.</summary>
    IActionRegistration<EquitySnapshot> OnEquityUpdated { get; }

    /// <summary>Fires when the backtest completes.</summary>
    IActionRegistration OnCompleted { get; }
}
