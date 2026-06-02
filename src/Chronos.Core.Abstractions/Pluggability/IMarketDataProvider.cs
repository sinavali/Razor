using Chronos.Core.Abstractions.Adapters;

namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>
/// Provides historical and live data independently of execution.
/// Allows using a data-only vendor for backtesting while executing through a different adapter.
/// </summary>
public interface IMarketDataProvider : IHistoricalDataProvider, ILiveDataProvider
{
    /// <summary>Name of the provider.</summary>
    string ProviderName { get; }
}
