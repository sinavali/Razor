namespace Chronos.Core.Abstractions.Shared;

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
