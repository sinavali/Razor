namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Market regimes.</summary>
public enum MarketRegime
{
    /// <summary>Unknown regime.</summary>
    Unknown,
    /// <summary>Trending regime.</summary>
    Trending,
    /// <summary>Range bound regime.</summary>
    RangeBound,
    /// <summary>High Volatility regime.</summary>
    HighVolatility,
    /// <summary>Low Volatility regime.</summary>
    LowVolatility,
    /// <summary>Breakout regime.</summary>
    Breakout
}
