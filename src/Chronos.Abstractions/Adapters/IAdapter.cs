using Chronos.Abstractions.Shared;

namespace Chronos.Abstractions.Adapters;

/// <summary>
/// Root interface bundling the capabilities of a specific brokerage or data feed.
/// </summary>
public interface IAdapter : IHistoricalDataProvider, ILiveDataProvider, IExecutionProvider
{
    /// <summary>Human‑readable adapter name.</summary>
    string AdapterName { get; }

    /// <summary>Exchange‑specific financial calculator.</summary>
    IMarketCalculator Calculator { get; }

    /// <summary>Indicates whether the adapter is currently connected to the broker/data feed.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Returns the timeframes supported by this adapter for the given symbol.
    /// Returns null if all timeframes are supported.
    /// </summary>
    TimeFrame[]? GetSupportedTimeframes(string symbol) => null;

    /// <summary>Establishes the underlying connection.</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Gracefully disconnects.</summary>
    Task DisconnectAsync();
}