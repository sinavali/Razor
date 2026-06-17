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
        if (step <= 0)
        {
            throw new ConfigurationException("SymbolProperties.MinVolume must be positive.");
        }

        double steps = Math.Round(requestedVolume / step, MidpointRounding.AwayFromZero);
        return steps * step;
    }

    /// <inheritdoc/>
    public virtual double NormalizePrice(SymbolProperties symbolProps, double requestedPrice)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        double tick = symbolProps.TickSize;
        if (tick <= 0)
        {
            throw new ConfigurationException("SymbolProperties.TickSize must be positive.");
        }

        double ticks = Math.Round(requestedPrice / tick, MidpointRounding.AwayFromZero);
        return ticks * tick;
    }

    /// <inheritdoc/>
    public virtual double CalculateRequiredMargin(SymbolProperties symbolProps, double price, double volume, double leverage)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        if (leverage <= 0)
        {
            leverage = 1;
        }

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
        double dailyRate = type == OrderType.Buy ? symbolProps.SwapLong : symbolProps.SwapShort;
        if (Math.Abs(dailyRate) < 1e-12)
        {
            return 0;
        }

        double days = (closeTime - openTime) / (double)TimeSpan.TicksPerDay;
        return volume * dailyRate * days;
    }

    /// <inheritdoc/>
    public virtual double CalculateFunding(SymbolProperties props, double volume, double openPrice, OrderType type,
                                          long currentTime, long lastFundingTime)
    {
        ArgumentNullException.ThrowIfNull(props);
        double fundingRate = props.FundingRate;
        if (Math.Abs(fundingRate) < 1e-12 || lastFundingTime >= currentTime)
        {
            return 0;
        }

        double periods = (currentTime - lastFundingTime) / (double)TimeSpan.TicksPerHour;
        double positionValue = openPrice * volume * props.ContractSize;
        double payment = positionValue * fundingRate * periods;
        return type == OrderType.Buy ? -payment : payment;
    }

    /// <inheritdoc/>
    public virtual bool IsPendingOrderTriggered(SymbolProperties props, OrderType pendingType,
                                               double bid, double ask, double orderPrice)
    {
        ArgumentNullException.ThrowIfNull(props);
        double triggerPrice = props.PendingTrigger switch
        {
            PendingOrderTriggerMode.UseBidForBuy => bid,
            PendingOrderTriggerMode.UseAskForBuy => ask,
            PendingOrderTriggerMode.UseMidPrice => (bid + ask) * 0.5,
            _ => ask
        };

        double sellTrigger = props.PendingTrigger switch
        {
            PendingOrderTriggerMode.UseBidForBuy => bid,
            PendingOrderTriggerMode.UseAskForBuy => ask,
            PendingOrderTriggerMode.UseMidPrice => (bid + ask) * 0.5,
            _ => bid
        };

        return pendingType switch
        {
            OrderType.BuyLimit => triggerPrice <= orderPrice,
            OrderType.SellLimit => sellTrigger >= orderPrice,
            OrderType.BuyStop => triggerPrice >= orderPrice,
            OrderType.SellStop => sellTrigger <= orderPrice,
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
