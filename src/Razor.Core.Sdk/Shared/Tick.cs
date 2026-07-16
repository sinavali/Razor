using System.Runtime.InteropServices;

namespace Razor.Core.Sdk.Shared;

/// <summary>
/// A single tick price update. Layout is sequential and packed for zero‑copy binary I/O.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Tick(long time, double bid, double ask, double volume, bool isSynthetic = false) : IEquatable<Tick>
{
    /// <summary>Timestamp in 100‑ns ticks.</summary>
    public readonly long Time = time;

    /// <summary>Bid price.</summary>
    public readonly double Bid = bid;

    /// <summary>Ask price.</summary>
    public readonly double Ask = ask;

    /// <summary>Tick volume.</summary>
    public readonly double Volume = volume;

    /// <summary>True if this tick was synthesised (e.g., from bar data).</summary>
    public readonly bool IsSynthetic = isSynthetic;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Tick other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(Tick other) =>
        Time == other.Time && Bid == other.Bid && Ask == other.Ask && Volume == other.Volume && IsSynthetic == other.IsSynthetic;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Time, Bid, Ask, Volume, IsSynthetic);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Tick left, Tick right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Tick left, Tick right) => !left.Equals(right);
}
