using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Adapters;

namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Context record for Risk Manager evaluation.</summary>
public sealed record RiskContext
{
    /// <summary>Current account balance.</summary>
    public required double Balance { get; init; }
    /// <summary>Current equity.</summary>
    public required double Equity { get; init; }
    /// <summary>Max drawdown recorded.</summary>
    public required double MaxDrawdown { get; init; }
    /// <summary>Max daily drawdown recorded.</summary>
    public required double MaxDailyDrawdown { get; init; }
    /// <summary>Margin used.</summary>
    public required double MarginUsed { get; init; }
    /// <summary>Free margin.</summary>
    public required double FreeMargin { get; init; }
    /// <summary>List of open positions.</summary>
    public required IReadOnlyList<Position> OpenPositions { get; init; }
    /// <summary>Order requested.</summary>
    public required AdapterOrderRequest? PendingOrder { get; init; }
    /// <summary>Symbol metadata.</summary>
    public required SymbolProperties? SymbolProperties { get; init; }
}
