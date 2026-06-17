using System.Collections.Immutable;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// A trading symbol and the timeframes the strategy consumes for it.
/// </summary>
public sealed record SymbolRequest
{
    /// <summary>Broker symbol (e.g., "EURUSD", "BTCUSDT").</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Timeframes the strategy consumes. Can include <see cref="TimeFrame.Tick"/> for raw tick feed.</summary>
    public ImmutableArray<TimeFrame> TimeFrames { get; init; } = ImmutableArray<TimeFrame>.Empty;

    /// <summary>Creates a new symbol request.</summary>
    public SymbolRequest(string symbol, ImmutableArray<TimeFrame> timeFrames)
    {
        Symbol = symbol;
        TimeFrames = timeFrames;
    }

    /// <summary>Parameterless constructor for record initialisation.</summary>
    public SymbolRequest()
    {
    }
}
