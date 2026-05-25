namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Base class for <see cref="IMarketCalculator"/> implementations.
/// Provides standard normalisation and margin/PnL logic that adapters can override
/// when an exchange requires different formulas.
/// </summary>
public abstract class DefaultMarketCalculator : IMarketCalculator
{
    /// <inheritdoc/>
    public virtual double NormalizeVolume(SymbolProperties symbolProps, double requestedVolume)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        double step = symbolProps.MinVolume;
        if (step <= 0) step = 0.0001;
        double steps = Math.Round(requestedVolume / step, MidpointRounding.AwayFromZero);
        return steps * step;
    }

    /// <inheritdoc/>
    public virtual double NormalizePrice(SymbolProperties symbolProps, double requestedPrice)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        double tick = symbolProps.TickSize;
        if (tick <= 0) tick = 0.01;
        double ticks = Math.Round(requestedPrice / tick, MidpointRounding.AwayFromZero);
        return ticks * tick;
    }

    /// <inheritdoc/>
    public virtual double CalculateRequiredMargin(SymbolProperties symbolProps, double price, double volume, double leverage)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        if (leverage <= 0) leverage = 1;
        return (price * volume * symbolProps.ContractSize) / leverage * symbolProps.InitialMarginRate;
    }

    /// <inheritdoc/>
    public virtual double CalculatePnL(SymbolProperties symbolProps, double entryPrice, double currentPrice, double volume, OrderType type)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        double diff = type == OrderType.Buy ? currentPrice - entryPrice : entryPrice - currentPrice;
        return diff * volume * symbolProps.TickValue / symbolProps.TickSize;
    }

    /// <inheritdoc/>
    public virtual double CalculateCommission(SymbolProperties symbolProps, double price, double volume)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        return price * volume * symbolProps.TakerFeeRate;
    }

    /// <inheritdoc/>
    public virtual double CalculateSwap(SymbolProperties symbolProps, double volume, OrderType type, long openTime, long closeTime)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        // Simplified: daily swap * days held
        double dailyRate = type == OrderType.Buy ? symbolProps.SwapLong : symbolProps.SwapShort;
        if (Math.Abs(dailyRate) < 1e-12) return 0;
        double days = (closeTime - openTime) / (double)TimeSpan.TicksPerDay;
        return volume * dailyRate * days;
    }

    /// <inheritdoc/>
    public virtual double CalculateFunding(SymbolProperties props, double volume, double openPrice, OrderType type,
                                          long currentTime, long lastFundingTime)
    {
        ArgumentNullException.ThrowIfNull(props);
        double fundingRate = props.FundingRate;
        if (Math.Abs(fundingRate) < 1e-12 || lastFundingTime >= currentTime) return 0;
        double periods = (currentTime - lastFundingTime) / (double)TimeSpan.TicksPerHour; // funding assumed per hour
        double positionValue = openPrice * volume * props.ContractSize;
        double payment = positionValue * fundingRate * periods;
        return type == OrderType.Buy ? -payment : payment;
    }

    /// <inheritdoc/>
    public virtual bool IsPendingOrderTriggered(SymbolProperties props, OrderType pendingType,
                                               double bid, double ask, double orderPrice)
    {
        ArgumentNullException.ThrowIfNull(props);
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
    public virtual double CalculateHoldingCost(SymbolProperties props, double volume, double openPrice, OrderType type,
                                              long fromTime, long toTime)
    {
        ArgumentNullException.ThrowIfNull(props);
        return CalculateSwap(props, volume, type, fromTime, toTime)
             + CalculateFunding(props, volume, openPrice, type, toTime, fromTime);
    }
}
