namespace Chronos.Core.Sdk.Shared;

/// <summary>
/// Base class for <see cref="IMarketCalculator"/> implementations.
/// Provides standard normalization and margin/PnL logic that adapters can override
/// when an exchange requires different formulas.
/// </summary>
public abstract class DefaultMarketCalculator : IMarketCalculator
{
    /// <inheritdoc/>
    public virtual double NormalizeVolume(SymbolProperties symbolProps, double requestedVolume)
    {
        ArgumentNullException.ThrowIfNull(symbolProps);
        // DAT‑07: Reject negative volume.
        if (requestedVolume < 0)
        {
            throw new ArgumentException("Volume cannot be negative.", nameof(requestedVolume));
        }

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
        // DAT‑07: Reject negative price.
        if (requestedPrice < 0)
        {
            throw new ArgumentException("Price cannot be negative.", nameof(requestedPrice));
        }

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

        // Use the interval defined in SymbolProperties (default to 1 hour if not set)
        long intervalTicks = props.HoldingCostIntervalTicks > 0 ? props.HoldingCostIntervalTicks : TimeSpan.TicksPerHour;
        double periods = (currentTime - lastFundingTime) / (double)intervalTicks;
        double positionValue = openPrice * volume * props.ContractSize;
        double payment = positionValue * fundingRate * periods;
        return type == OrderType.Buy ? -payment : payment;
    }

    /// <inheritdoc/>
    public virtual bool IsPendingOrderTriggered(SymbolProperties props, OrderType pendingType,
                                               double bid, double ask, double orderPrice)
    {
        ArgumentNullException.ThrowIfNull(props);

        // For buy orders, use the trigger mode to choose bid or ask.
        // For sell orders, always use ask for limit/stop checks (exchange convention).
        double buyTriggerPrice = props.PendingTrigger switch
        {
            PendingOrderTriggerMode.UseBidForBuy => bid,
            PendingOrderTriggerMode.UseAskForBuy => ask,
            PendingOrderTriggerMode.UseMidPrice => (bid + ask) * 0.5,
            _ => ask
        };

        double sellTriggerPrice = props.PendingTrigger switch
        {
            // For sell orders, we typically use ask for limit and bid for stop,
            // but we simplify by using ask for limit and bid for stop.
            // More precise: SellLimit triggers when ask >= orderPrice, SellStop when bid <= orderPrice.
            _ => (pendingType == OrderType.SellLimit) ? ask : bid
        };

        return pendingType switch
        {
            OrderType.BuyLimit => buyTriggerPrice <= orderPrice,
            OrderType.SellLimit => sellTriggerPrice >= orderPrice,
            OrderType.BuyStop => buyTriggerPrice >= orderPrice,
            OrderType.SellStop => sellTriggerPrice <= orderPrice,
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

    /// <inheritdoc/>
    public virtual double CalculateSlippage(SymbolProperties props, OrderType type, double volume, double price)
    {
        ArgumentNullException.ThrowIfNull(props);
        return 0.0;
    }
}
