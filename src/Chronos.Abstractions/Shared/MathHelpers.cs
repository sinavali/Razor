namespace Chronos.Abstractions.Shared;

/// <summary>Math utilities.</summary>
public static class MathHelpers
{
    /// <summary>Clamps a value to the inclusive range.</summary>
    public static double ClampToRange(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    /// <summary>Calculates percentage change from a peak.</summary>
    public static double CalculatePercentageChange(double peak, double current)
        => peak > 1e-8 ? (peak - current) / peak * 100.0 : 0.0;
}