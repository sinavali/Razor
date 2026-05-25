namespace Chronos.Core.Abstractions.Shared;

/// <summary>Exchange‑specific symbol properties. All fields must be explicitly set by the adapter.</summary>
public sealed record SymbolProperties
{
    /// <summary>Asset class.</summary>
    public required AssetClass AssetClass { get; init; }
    /// <summary>Margin mode.</summary>
    public required MarginMode MarginMode { get; init; }
    /// <summary>Pending order trigger logic.</summary>
    public required PendingOrderTriggerMode PendingTrigger { get; init; }
    /// <summary>Currency used for margin calculations (e.g. USDT).</summary>
    public required string MarginCurrency { get; init; }
    /// <summary>Contract size.</summary>
    public required double ContractSize { get; init; }
    /// <summary>Minimum price increment.</summary>
    public required double TickSize { get; init; }
    /// <summary>Monetary value of one tick.</summary>
    public required double TickValue { get; init; }
    /// <summary>Minimum order volume.</summary>
    public required double MinVolume { get; init; }
    /// <summary>Maximum allowed leverage.</summary>
    public required double MaxLeverage { get; init; }
    /// <summary>Swap rate for long positions (per day).</summary>
    public required double SwapLong { get; init; }
    /// <summary>Swap rate for short positions (per day).</summary>
    public required double SwapShort { get; init; }
    /// <summary>Hour (UTC) at which swap/rollover is charged.</summary>
    public required int SwapRolloverHourUtc { get; init; }
    /// <summary>Multiplier for triple‑swap days.</summary>
    public required double TripleSwapDayMultiplier { get; init; }
    /// <summary>Funding rate for perpetual contracts.</summary>
    public required double FundingRate { get; init; }
    /// <summary>Initial margin rate (fraction).</summary>
    public required double InitialMarginRate { get; init; }
    /// <summary>Maintenance margin rate (fraction).</summary>
    public required double MaintenanceMarginRate { get; init; }
    /// <summary>Maker fee rate (fraction).</summary>
    public required double MakerFeeRate { get; init; }
    /// <summary>Taker fee rate (fraction).</summary>
    public required double TakerFeeRate { get; init; }
}
