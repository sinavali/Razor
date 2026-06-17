namespace Chronos.Core.Abstractions.Shared;

/// <summary>Immutable data request provided to report generators.</summary>
public sealed record ReportRequest
{
    /// <summary>Title of the report.</summary>
    public required string ReportTitle { get; init; }

    /// <summary>Chronological trade history.</summary>
    public required IReadOnlyList<Position> TradeHistory { get; init; }

    /// <summary>Starting balance.</summary>
    public required double InitialBalance { get; init; }

    /// <summary>Ending balance.</summary>
    public required double FinalBalance { get; init; }

    /// <summary>Maximum drawdown.</summary>
    public required double MaxDrawdown { get; init; }

    /// <summary>Start date of analysis.</summary>
    public required DateTime StartDate { get; init; }

    /// <summary>End date of analysis.</summary>
    public required DateTime EndDate { get; init; }

    /// <summary>Additional arbitrary metrics injected by hooks.</summary>
    public IReadOnlyDictionary<string, double> AdditionalMetrics { get; init; } = new Dictionary<string, double>();
}
