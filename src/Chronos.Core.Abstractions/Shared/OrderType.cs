namespace Chronos.Core.Abstractions.Shared;

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
