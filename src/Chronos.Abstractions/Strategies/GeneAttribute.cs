namespace Chronos.Abstractions.Strategies;

/// <summary>Type of gene (affects mutation behaviour).</summary>
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
    public string Name { get; set; } = string.Empty;
    /// <summary>Deterministic ordering index (lower = earlier in chromosome).</summary>
    public int Order { get; set; }

    /// <summary>Creates a new gene attribute.</summary>
    public GeneAttribute(double min, double max, double step = 1.0, GeneType type = GeneType.Continuous)
    {
        if (min > max) throw new ArgumentException("A gene cannot have a minimum value greater than its maximum value.");
        if (step < 0) throw new ArgumentException("Gene step must be non‑negative.");
        if ((type == GeneType.Categorical || type == GeneType.Discrete) && step < 1)
            throw new ArgumentException("Categorical and Discrete genes must have a step of at least 1.");
        Min = min;
        Max = max;
        Step = step;
        Type = type;
    }
}