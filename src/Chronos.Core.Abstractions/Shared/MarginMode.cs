namespace Chronos.Core.Abstractions.Shared;

/// <summary>Margin mode for the account or symbol.</summary>
public enum MarginMode
{
    /// <summary>Cross margin (shared across positions).</summary>
    Cross,
    /// <summary>Isolated margin (per position).</summary>
    Isolated
}
