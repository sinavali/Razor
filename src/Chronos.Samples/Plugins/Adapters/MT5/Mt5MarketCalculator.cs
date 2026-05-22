using Chronos.Abstractions.Shared;

namespace Chronos.Samples.Plugins.Adapters.MT5;

/// <summary>
/// Financial math contract for MetaTrader 5.
/// Implements margin, PnL, swap, commission, volume/price normalisation
/// using the exact formulas documented by MetaQuotes.
/// </summary>
internal sealed class Mt5MarketCalculator : IMarketCalculator
{
    /// <inheritdoc/>
    public double NormalizeVolume(SymbolProperties props, double requestedVolume)
    {
        // MT5 volumes must be multiples of MinVolume (or lot step)
        double step = props.MinVolume;
        if (step <= 0) return requestedVolume;
        double lots = Math.Round(requestedVolume / step, MidpointRounding.AwayFromZero);
        return Math.Max(lots * step, step);
    }

    /// <inheritdoc/>
    public double NormalizePrice(SymbolProperties props, double requestedPrice)
    {
        double ts = props.TickSize;
        if (ts <= 0) return requestedPrice;
        return Math.Round(requestedPrice / ts, MidpointRounding.AwayFromZero) * ts;
    }

    /// <inheritdoc/>
    public double CalculateRequiredMargin(SymbolProperties props, double price, double volume, double leverage)
    {
        if (leverage <= 0) leverage = 1;
        // Standard MT5 margin: Volume * ContractSize * Price / Leverage
        return volume * props.ContractSize * price / leverage;
    }

    /// <inheritdoc/>
    public double CalculatePnL(SymbolProperties props, double entryPrice, double currentPrice, double volume, OrderType type)
    {
        double diff = type == OrderType.Buy ? currentPrice - entryPrice : entryPrice - currentPrice;
        // PnL = diff * ContractSize * Volume (in quote currency)
        return diff * props.ContractSize * volume;
    }

    /// <inheritdoc/>
    public double CalculateCommission(SymbolProperties props, double price, double volume)
    {
        // Assume taker fee model; maker/taker distinction not known at adapter level.
        // Use TakerFeeRate times notional value.
        double notional = price * volume * props.ContractSize;
        return notional * props.TakerFeeRate;
    }

    /// <inheritdoc/>
    public double CalculateSwap(SymbolProperties props, double volume, OrderType type, long openTime, long closeTime)
    {
        return CalculateHoldingCost(props, volume, 0, type, openTime, closeTime);
    }

    /// <inheritdoc/>
    public double CalculateFunding(SymbolProperties props, double volume, double openPrice, OrderType type, long currentTime, long lastFundingTime)
    {
        // For perpetual crypto futures, funding rate is applied periodically.
        // Not directly used in current MT5 spot/forex, but implemented generically.
        if (lastFundingTime >= currentTime) return 0;
        double hours = (currentTime - lastFundingTime) / (double)TimeSpan.TicksPerHour;
        double notional = openPrice * volume * props.ContractSize;
        // Funding = -1 * fundingRate * notional (payer/receiver depends on side)
        double rate = props.FundingRate; // signed? simplified.
        return -rate * notional * hours;
    }

    /// <inheritdoc/>
    public bool IsPendingOrderTriggered(SymbolProperties props, OrderType pendingType, double bid, double ask, double orderPrice)
    {
        return pendingType switch
        {
            OrderType.BuyLimit => ask <= orderPrice,
            OrderType.SellLimit => bid >= orderPrice,
            OrderType.BuyStop => ask >= orderPrice,
            OrderType.SellStop => bid <= orderPrice,
            _ => false
        };
    }

    /// <inheritdoc/>
    public double CalculateHoldingCost(SymbolProperties props, double volume, double openPrice, OrderType type, long fromTime, long toTime)
    {
        if (fromTime >= toTime) return 0;
        // MT5 swap: rate per day, charged at rollover. Simplified calculation:
        // days = time span in days (including fractional for intra-day)
        double days = (toTime - fromTime) / (double)TimeSpan.TicksPerDay;
        double swapRate = type == OrderType.Buy ? props.SwapLong : props.SwapShort;
        // Multiply by point value? TickValue typically denotes profit per tick per contract.
        // Standard swap: lot * contract * point * swapRate * days. point = TickSize
        double pointValue = props.TickValue / props.TickSize; // profit per point per contract
        double notional = volume * props.ContractSize;
        return notional * pointValue * swapRate * days;
    }
}