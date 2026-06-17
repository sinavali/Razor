namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// OHLCV bar used only for converting external chart data to synthetic ticks.
/// <para>
/// <c>OpenTime</c> is inclusive; <c>CloseTime</c> is inclusive. Both represent the same
/// exchange‑reported timestamps without modification.
/// </para>
/// </summary>
public readonly struct Bar : IEquatable<Bar>
{
    /// <summary>Opening time (inclusive).</summary>
    public long OpenTime { get; }

    /// <summary>Open price.</summary>
    public double Open { get; }

    /// <summary>High price.</summary>
    public double High { get; }

    /// <summary>Low price.</summary>
    public double Low { get; }

    /// <summary>Close price.</summary>
    public double Close { get; }

    /// <summary>Total volume.</summary>
    public double Volume { get; }

    /// <summary>Closing time (inclusive).</summary>
    public long CloseTime { get; }

    /// <summary>Creates a new bar.</summary>
    public Bar(long openTime, double open, double high, double low, double close, double volume, long closeTime)
    {
        OpenTime = openTime;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        CloseTime = closeTime;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Bar other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(Bar other) =>
        OpenTime == other.OpenTime && Open == other.Open && High == other.High &&
        Low == other.Low && Close == other.Close && Volume == other.Volume && CloseTime == other.CloseTime;

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(OpenTime, Open, High, Low, Close, Volume, CloseTime);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Bar left, Bar right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Bar left, Bar right) => !left.Equals(right);
}
