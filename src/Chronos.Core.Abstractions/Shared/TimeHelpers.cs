namespace Chronos.Core.Abstractions.Shared;

/// <summary>UTC‑based, stateless time helpers (pure, no system clock).</summary>
public static class TimeHelpers
{
    /// <summary>Converts Unix seconds to UTC ticks.</summary>
    public static long FromUnixSeconds(long unixSeconds)
        => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcTicks;

    /// <summary>Converts Unix milliseconds to UTC ticks.</summary>
    public static long FromUnixMilliseconds(long unixMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcTicks;

    /// <summary>Converts a Unix timestamp to a UTC DateTime.</summary>
    public static DateTime UnixToDateTime(long unixTimestamp)
        => DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;

    /// <summary>Converts a DateTime to a Unix timestamp.</summary>
    public static long DateTimeToUnix(DateTime dateTime)
        => new DateTimeOffset(dateTime).ToUnixTimeSeconds();
}
