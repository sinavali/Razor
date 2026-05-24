namespace Chronos.Core.Abstractions.Adapters;

/// <summary>
/// Handles order execution and position management at the exchange level.
/// Implementations must be thread‑safe.
/// </summary>
public interface IExecutionProvider
{
    /// <summary>Submits a new order.</summary>
    Task<Chronos.Core.Abstractions.Shared.AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest request);
    /// <summary>Modifies an existing order's SL, TP, or limit/stop price.</summary>
    Task<Chronos.Core.Abstractions.Shared.AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null);
    /// <summary>Closes a position (or partially closes it).</summary>
    Task<Chronos.Core.Abstractions.Shared.AdapterOrderResponse> ClosePositionAsync(long ticket, double? volume = null);
    /// <summary>Cancels a pending order.</summary>
    Task<Chronos.Core.Abstractions.Shared.AdapterOrderResponse> CancelAsync(long ticket);
    /// <summary>Returns current account balance and equity.</summary>
    Task<(double Balance, double Equity)> GetAccountInfoAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns all currently open positions.</summary>
    Task<IReadOnlyList<Chronos.Core.Abstractions.Shared.Position>> GetActivePositionsAsync();
    /// <summary>Returns all currently pending orders.</summary>
    Task<IReadOnlyList<Chronos.Core.Abstractions.Shared.Order>> GetPendingOrdersAsync();
    /// <summary>Fetches symbol properties from the exchange.</summary>
    Task<Chronos.Core.Abstractions.Shared.SymbolProperties?> GetSymbolPropertiesAsync(string symbol, CancellationToken cancellationToken = default);
    /// <summary>Raised when a dynamic execution update is received.</summary>
    event Action<ExecutionReport> OnExecutionUpdate;
}
