using Chronos.Abstractions.Shared;

namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Abstraction of a trading account, whether simulated or live.
/// Provides stateless order entry, position query, and account metrics.
/// </summary>
public interface IBroker
{
    /// <summary>Current account balance.</summary>
    double Balance { get; }
    /// <summary>Current equity (balance + floating P&amp;L).</summary>
    double Equity { get; }
    /// <summary>Margin currently in use.</summary>
    double MarginUsed { get; }
    /// <summary>Free margin available for new positions.</summary>
    double FreeMargin { get; }
    /// <summary>Maximum drawdown percentage observed.</summary>
    double MaxDrawdown { get; }
    /// <summary>Maximum daily drawdown percentage observed.</summary>
    double MaxDailyDrawdown { get; }

    /// <summary>Syncs live state (balance, positions, orders) with the exchange.</summary>
    Task InitializeLiveStateAsync(CancellationToken cancellationToken);
    /// <summary>Periodically reconciles state with the exchange.</summary>
    Task SyncStateAsync(CancellationToken cancellationToken);

    /// <summary>Places a market order (async, non‑blocking).</summary>
    Task<AdapterOrderResponse> ExecuteMarketOrderAsync(string symbol, OrderType type, double volume,
        double sl = 0, double tp = 0, string comment = "");
    /// <summary>Places a pending order (limit/stop).</summary>
    Task<AdapterOrderResponse> PlacePendingOrderAsync(string symbol, OrderType type, double volume,
        double price, double sl, double tp, string comment = "");
    /// <summary>Modifies an existing pending order's stop loss, take profit, or price.</summary>
    Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null);
    /// <summary>Cancels a pending order.</summary>
    Task<AdapterOrderResponse> CancelOrderAsync(long ticket);
    /// <summary>Closes a specific position by ticket.</summary>
    Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double volume = 0);
    /// <summary>Closes all positions for a symbol, optionally filtered by type.</summary>
    Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(string symbol, OrderType? type = null);

    /// <summary>Checks if any open position exists for the symbol (and type).</summary>
    Task<bool> HasOpenPositionAsync(string symbol, OrderType? type = null, CancellationToken cancellationToken = default);
    /// <summary>Returns all open positions, optionally filtered by symbol.</summary>
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? symbol = null, CancellationToken cancellationToken = default);
    /// <summary>Returns the complete historical trade list.</summary>
    Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns all currently pending orders.</summary>
    Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken cancellationToken = default);
}