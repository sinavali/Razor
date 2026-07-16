namespace Razor.Core.Sdk.Shared;

/// <summary>
/// A single entry recorded for reinforcement learning or behavioral analysis.
/// </summary>
public sealed record BehaviorRecord
{
    /// <summary>UTC timestamp of the recorded event.</summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Unique session identifier (backtest or live).</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Name of the strategy that generated this record.</summary>
    public string StrategyName { get; init; } = string.Empty;

    /// <summary>Action taken by the strategy (e.g., "Buy", "Sell", "Close", "None").</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Optional reward signal associated with the action.</summary>
    public double? Reward { get; init; }

    /// <summary>
    /// State dictionary containing indicator values, open positions, equity, etc.
    /// The strategy is responsible for populating this.
    /// </summary>
    public Dictionary<string, double> State { get; init; } = new();
}
