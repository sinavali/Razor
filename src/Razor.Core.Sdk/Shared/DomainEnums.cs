namespace Razor.Core.Sdk.Shared;

/// <summary>Financial instrument class.</summary>
public enum AssetClass
{
    /// <summary>Foreign exchange.</summary>
    Forex,

    /// <summary>Spot cryptocurrency.</summary>
    CryptoSpot,

    /// <summary>Perpetual cryptocurrency futures.</summary>
    CryptoPerpetual,

    /// <summary>Equity (stocks).</summary>
    Equity,

    /// <summary>Futures contract.</summary>
    Future,

    /// <summary>Contract for difference.</summary>
    CFD
}

/// <summary>Margin mode for the account or symbol.</summary>
public enum MarginMode
{
    /// <summary>Cross margin (shared across positions).</summary>
    Cross,

    /// <summary>Isolated margin (per position).</summary>
    Isolated
}

/// <summary>Pending order trigger logic.</summary>
public enum PendingOrderTriggerMode
{
    /// <summary>Use the bid price for buy orders.</summary>
    UseBidForBuy,

    /// <summary>Use the ask price for buy orders.</summary>
    UseAskForBuy,

    /// <summary>Use the mid‑price for all orders.</summary>
    UseMidPrice
}

/// <summary>Order type.</summary>
public enum OrderType
{
    /// <summary>Market buy.</summary>
    Buy,

    /// <summary>Market sell.</summary>
    Sell,

    /// <summary>Buy limit order.</summary>
    BuyLimit,

    /// <summary>Sell limit order.</summary>
    SellLimit,

    /// <summary>Buy stop order.</summary>
    BuyStop,

    /// <summary>Sell stop order.</summary>
    SellStop
}
