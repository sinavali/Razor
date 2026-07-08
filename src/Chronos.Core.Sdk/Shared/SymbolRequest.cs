using System.Collections.Immutable;

namespace Chronos.Core.Sdk.Shared;

/// <summary>
/// A trading symbol and the timeframes the strategy consumes for it.
/// </summary>
public sealed record SymbolRequest : IEquatable<SymbolRequest>
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

    /// <summary>Override equality to compare content, not reference.</summary>
    public bool Equals(SymbolRequest? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase)
            && TimeFrames.SequenceEqual(other.TimeFrames);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Symbol, StringComparer.OrdinalIgnoreCase);
        foreach (var tf in TimeFrames)
        {
            hash.Add(tf);
        }
        return hash.ToHashCode();
    }
}
