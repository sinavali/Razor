namespace Razor.Core.Sdk.Hooks;

/// <summary>
/// Snapshot of account equity state at a point in time.
/// </summary>
public readonly record struct EquitySnapshot(
    double Equity,
    double Balance,
    double Drawdown,
    double DailyDrawdown
);
