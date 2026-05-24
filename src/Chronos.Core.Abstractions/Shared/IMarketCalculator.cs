namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Stateless, O(1) financial math contract provided by the adapter.
/// Encapsulates exchange‑specific volume/price normalisation and PnL calculations.
/// </summary>
public interface IMarketCalculator
{
    /// <summary>Normalise volume to exchange step size.</summary>
    double NormalizeVolume(SymbolProperties symbolProps, double requestedVolume);
    /// <summary>Normalise price to exchange‑step size (including tick‑table awareness).</summary>
    double NormalizePrice(SymbolProperties symbolProps, double requestedPrice);
    /// <summary>Calculate required margin for a position.</summary>
    double CalculateRequiredMargin(SymbolProperties symbolProps, double price, double volume, double leverage);
    /// <summary>Calculate unrealised PnL.</summary>
    double CalculatePnL(SymbolProperties symbolProps, double entryPrice, double currentPrice, double volume, OrderType type);
    /// <summary>Calculate commission for a fill.</summary>
    double CalculateCommission(SymbolProperties symbolProps, double price, double volume);
    /// <summary>Calculate swap / funding cost for an open position between two timestamps.</summary>
    double CalculateSwap(SymbolProperties symbolProps, double volume, OrderType type, long openTime, long closeTime);
    /// <summary>
    /// Applies periodic funding / dividend / holding cost for a position.
    /// Returns the cost amount (negative means payment from trader).
    /// </summary>
    double CalculateFunding(SymbolProperties props, double volume, double openPrice, OrderType type,
                           long currentTime, long lastFundingTime);
    /// <summary>
    /// Determines if a pending order should be triggered by the given bid/ask.
    /// Encapsulates exchange‑specific trigger rules.
    /// </summary>
    bool IsPendingOrderTriggered(SymbolProperties props, OrderType pendingType,
                                 double bid, double ask, double orderPrice);
    /// <summary>
    /// Calculates the holding cost (swap/funding) for an open position between two timestamps.
    /// </summary>
    double CalculateHoldingCost(SymbolProperties props, double volume, double openPrice, OrderType type,
                                long fromTime, long toTime);
}
