namespace Chronos.Core.Abstractions.Shared;

/// <summary>Exchange‑specific symbol properties.</summary>
public sealed record SymbolProperties
{
    /// <summary>Asset class.</summary>
    public AssetClass AssetClass { get; init; } = AssetClass.CryptoSpot;
    /// <summary>Margin mode.</summary>
    public MarginMode MarginMode { get; init; } = MarginMode.Cross;
    /// <summary>Pending order trigger logic.</summary>
    public PendingOrderTriggerMode PendingTrigger { get; init; } = PendingOrderTriggerMode.UseAskForBuy;
    /// <summary>Currency used for margin calculations (e.g. USDT).</summary>
    public string MarginCurrency { get; init; } = "USDT";
    /// <summary>Contract size.</summary>
    public double ContractSize { get; init; } = 1.0;
    /// <summary>Minimum price increment.</summary>
    public double TickSize { get; init; } = 0.01;
    /// <summary>Monetary value of one tick.</summary>
    public double TickValue { get; init; } = 1.0;
    /// <summary>Minimum order volume.</summary>
    public double MinVolume { get; init; } = 0.0001;
    /// <summary>Maximum allowed leverage.</summary>
    public double MaxLeverage { get; init; } = 1.0;
    /// <summary>Swap rate for long positions (per day).</summary>
    public double SwapLong { get; init; }
    /// <summary>Swap rate for short positions (per day).</summary>
    public double SwapShort { get; init; }
    /// <summary>Hour (UTC) at which swap/rollover is charged.</summary>
    public int SwapRolloverHourUtc { get; init; } = 21;
    /// <summary>Multiplier for triple‑swap days.</summary>
    public double TripleSwapDayMultiplier { get; init; } = 3.0;
    /// <summary>Funding rate for perpetual contracts.</summary>
    public double FundingRate { get; init; }
    /// <summary>Initial margin rate (fraction).</summary>
    public double InitialMarginRate { get; init; } = 1.0;
    /// <summary>Maintenance margin rate (fraction).</summary>
    public double MaintenanceMarginRate { get; init; } = 0.5;
    /// <summary>Maker fee rate (fraction).</summary>
    public double MakerFeeRate { get; init; } = 0.001;
    /// <summary>Taker fee rate (fraction).</summary>
    public double TakerFeeRate { get; init; } = 0.0015;
}
