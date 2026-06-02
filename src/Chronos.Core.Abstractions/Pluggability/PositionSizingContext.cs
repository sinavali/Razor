using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Context record for Position Sizer.</summary>
public sealed record PositionSizingContext
{
    /// <summary>Account equity.</summary>
    public required double AccountEquity { get; init; }
    /// <summary>Risk percentage to apply.</summary>
    public required double RiskPercent { get; init; }
    /// <summary>Stop loss distance.</summary>
    public required double StopLossDistance { get; init; }
    /// <summary>Symbol metadata.</summary>
    public required SymbolProperties SymbolProperties { get; init; }
    /// <summary>Entry price.</summary>
    public required double EntryPrice { get; init; }
    /// <summary>Account leverage.</summary>
    public required double Leverage { get; init; }
}
