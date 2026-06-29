namespace Chronos.Core.Abstractions.Shared;

/// <summary>Type of gene (affects mutation behavior).</summary>
public enum GeneType
{
    /// <summary>Continuous range.</summary>
    Continuous,
    /// <summary>Discrete steps.</summary>
    Discrete,
    /// <summary>Categorical choice.</summary>
    Categorical,
    /// <summary>Structural gene (e.g., topology).</summary>
    Structural,
    /// <summary>Parametric gene (e.g., neural weight).</summary>
    Parametric
}

/// <summary>
/// Marks a strategy property as an optimizable gene.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class GeneAttribute : Attribute
{
    /// <summary>Minimum value.</summary>
    public double Min { get; }
    /// <summary>Maximum value.</summary>
    public double Max { get; }
    /// <summary>Step size (for discrete genes).</summary>
    public double Step { get; }
    /// <summary>Gene type.</summary>
    public GeneType Type { get; }
    /// <summary>Display name (optional).</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Deterministic ordering index (lower = earlier in chromosome).</summary>
    public int Order { get; init; }

    /// <summary>Creates a new gene attribute.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when constraints are invalid or when a non‑zero <paramref name="step"/> is supplied
    /// for a <see cref="GeneType"/> that does not support stepping
    /// (<see cref="GeneType.Continuous"/>, <see cref="GeneType.Structural"/>, <see cref="GeneType.Parametric"/>).
    /// </exception>
    public GeneAttribute(double min, double max, double step = 0.0, GeneType type = GeneType.Continuous)
    {
        if (min > max)
        {
            throw new ArgumentException($"Gene minimum value ({min}) cannot be greater than maximum value ({max}).");
        }

        if (step < 0)
        {
            throw new ArgumentException($"Gene step must be non-negative. Provided: {step}.");
        }

        // For Continuous, Structural, and Parametric genes, stepping is not applied.
        if (type is GeneType.Continuous or GeneType.Structural or GeneType.Parametric)
        {
            if (step != 0)
            {
                throw new ArgumentException($"Step must be 0 for {type} genes. Provided: {step}. " +
                                              "Only Discrete and Categorical genes support non-zero steps.");
            }
        }
        else // Discrete or Categorical
        {
            if (step <= 0)
            {
                throw new ArgumentException($"Step must be positive for {type} genes. Provided: {step}. " +
                                              "Discrete and Categorical genes require a positive step size.");
            }
        }

        Min = min;
        Max = max;
        Step = step;
        Type = type;
    }
}
