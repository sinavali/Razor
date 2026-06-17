using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Slots;

/// <summary>
/// Capability interface for adapters that connect Chronos to a broker or exchange.
/// An adapter declares which sub‑capabilities it supports via the boolean flags.
/// Implementations are discovered in the <c>Adapters/</c> directory.
/// </summary>
public interface IAdapterCapability
{
    /// <summary>Human‑readable adapter name.</summary>
    string Name { get; }

    /// <summary>Exchange‑specific financial calculator.</summary>
    IMarketCalculator Calculator { get; }

    /// <summary>Whether the adapter is currently connected.</summary>
    bool IsConnected { get; }

    // ── capability flags ──────────────────────────────────────────

    /// <summary>Whether this adapter can provide historical tick data.</summary>
    bool SupportsHistoricalData { get; }

    /// <summary>Whether this adapter can stream live tick data.</summary>
    bool SupportsLiveData { get; }

    /// <summary>Whether this adapter can execute orders.</summary>
    bool SupportsExecution { get; }

    // ── connection ─────────────────────────────────────────────────

    /// <summary>Establishes the underlying connection.</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Gracefully disconnects.</summary>
    Task DisconnectAsync();

    // ── historical data ───────────────────────────────────────────

    /// <summary>Fetches history and writes it to a binary file.</summary>
    Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(HistoricalDataRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a previously cached binary file.</summary>
    Task DeleteHistoryFileAsync(string filePath);

    /// <summary>
    /// Called by Chronos after it has finished reading the binary file.
    /// The adapter may now delete it if its retention policy allows.
    /// </summary>
    Task NotifyFileSafeToDeleteAsync(string filePath);

    // ── live data ─────────────────────────────────────────────────

    /// <summary>Subscribes to tick updates for the given symbol.</summary>
    Task SubscribeAsync(string symbol);

    /// <summary>Unsubscribes from tick updates.</summary>
    Task UnsubscribeAsync(string symbol);

    /// <summary>Raised for every received tick.</summary>
    event Action<string, Tick> OnTickReceived;

    // ── execution ─────────────────────────────────────────────────

    /// <summary>Submits a new order.</summary>
    Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest request);

    /// <summary>Modifies an existing order's SL, TP, or limit/stop price.</summary>
    Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null);

    /// <summary>Closes a position (or partially closes it).</summary>
    Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double? volume = null);

    /// <summary>Cancels a pending order.</summary>
    Task<AdapterOrderResponse> CancelAsync(long ticket);

    /// <summary>Returns current account balance and equity.</summary>
    Task<(double Balance, double Equity)> GetAccountInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all currently open positions.</summary>
    Task<IReadOnlyList<Position>> GetActivePositionsAsync();

    /// <summary>Returns all currently pending orders.</summary>
    Task<IReadOnlyList<Order>> GetPendingOrdersAsync();

    /// <summary>Fetches symbol properties from the exchange.</summary>
    Task<SymbolProperties?> GetSymbolPropertiesAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>Raised when a dynamic execution update is received.</summary>
    event Action<ExecutionReport> OnExecutionUpdate;

    // ── symbol support ────────────────────────────────────────────

    /// <summary>
    /// Returns the timeframes supported by this adapter for the given symbol.
    /// Returns null if all timeframes are supported.
    /// </summary>
    TimeFrame[]? GetSupportedTimeframes(string symbol);
}
