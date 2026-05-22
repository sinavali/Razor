namespace Chronos.Abstractions.Shared;

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