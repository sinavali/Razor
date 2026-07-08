namespace Chronos.Core.Shared;

/// <summary>Math utilities.</summary>
public static class MathHelpers
{
    /// <summary>Clamps a value to the inclusive range.</summary>
    public static double ClampToRange(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    /// <summary>Calculates percentage change from a peak.</summary>
    public static double CalculatePercentageChange(double peak, double current)
        => peak > 1e-8 ? (peak - current) / peak * 100.0 : 0.0;
}

/// <summary>Argument validation helpers.</summary>
public static class ValidationHelpers
{
    /// <summary>Throws if value is not positive.</summary>
    public static void ValidatePositive(double value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{paramName} must be positive.", paramName);
        }
    }

    /// <summary>Throws if value is outside the inclusive range.</summary>
    public static void ValidateRange(double value, double min, double max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be between {min} and {max}.");
        }
    }

    /// <summary>Throws if value is null.</summary>
    public static void ValidateNotNull<T>(T value, string paramName) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}

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
