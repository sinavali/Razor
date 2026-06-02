using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Classifies the current market into a discrete regime.</summary>
public interface IMarketRegimeDetector
{
    /// <summary>Detects the current market regime.</summary>
    MarketRegime Detect(string symbol, TickWindow tickWindow);
}
