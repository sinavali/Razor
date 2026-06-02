using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Execution Algorithm Request.</summary>
public sealed record ExecutionAlgorithmRequest
{
    /// <summary>Symbol.</summary>
    public required string Symbol { get; init; }
    /// <summary>Trade direction.</summary>
    public required OrderType Direction { get; init; }
    /// <summary>Total volume.</summary>
    public required double TotalVolume { get; init; }
    /// <summary>Start time.</summary>
    public required DateTime StartTime { get; init; }
    /// <summary>End time.</summary>
    public required DateTime EndTime { get; init; }
    /// <summary>Symbol properties.</summary>
    public required SymbolProperties SymbolProperties { get; init; }
}
