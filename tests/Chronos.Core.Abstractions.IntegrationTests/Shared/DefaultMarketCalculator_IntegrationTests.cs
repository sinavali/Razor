using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

internal sealed class IntegrationTestableCalculator : DefaultMarketCalculator
{
}

public class DefaultMarketCalculator_IntegrationTests
{
    private readonly IntegrationTestableCalculator _calc = new();

    private SymbolProperties CreateProps(double tickSize = 0.01, double minVolume = 0.01, double contractSize = 100_000,
        double initialMarginRate = 0.02, double takerFee = 0.002, double swapLong = 10, double fundingRate = 0.001)
        => new()
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
            MarginCurrency = "USD",
            ContractSize = contractSize,
            TickSize = tickSize,
            TickValue = 1,
            MinVolume = minVolume,
            MaxLeverage = 100,
            SwapLong = swapLong,
            SwapShort = 5,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = fundingRate,
            InitialMarginRate = initialMarginRate,
            MaintenanceMarginRate = 0.01,
            MakerFeeRate = 0.001,
            TakerFeeRate = takerFee
        };

    private static SymbolProperties MakeProps(PendingOrderTriggerMode mode = PendingOrderTriggerMode.UseAskForBuy)
    {
        return new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = mode,
            MarginCurrency = "USD",
            ContractSize = 100000,
            TickSize = 0.0001,
            TickValue = 1,
            MinVolume = 0.01,
            MaxLeverage = 100,
            SwapLong = 5,
            SwapShort = -3,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = 0.001,
            InitialMarginRate = 0.02,
            MaintenanceMarginRate = 0.01,
            MakerFeeRate = 0.0005,
            TakerFeeRate = 0.001
        };
    }

    [Fact]
    public void Full_Calculation_Pipeline()
    {
        var props = CreateProps();

        double volume = _calc.NormalizeVolume(props, 0.12);
        double price = _calc.NormalizePrice(props, 1.23456);
        Assert.Equal(0.12, volume, 5);
        Assert.Equal(1.23, price, 2);

        double margin = _calc.CalculateRequiredMargin(props, price, volume, 10);
        Assert.True(margin > 0);

        double pnl = _calc.CalculatePnL(props, price, price + 0.01, volume, OrderType.Buy);
        Assert.True(pnl > 0);

        double comm = _calc.CalculateCommission(props, price, volume);
        Assert.True(comm >= 0);

        long oneDay = TimeSpan.TicksPerDay;
        double swap = _calc.CalculateSwap(props, volume, OrderType.Buy, 0, oneDay);
        Assert.Equal(volume * 10, swap, 10);

        double funding = _calc.CalculateFunding(props, volume, price, OrderType.Sell, oneDay, 0);
        Assert.True(funding > 0);

        double holding = _calc.CalculateHoldingCost(props, volume, price, OrderType.Buy, 0, oneDay);
        Assert.True(holding < 0);

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.20, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0, 1.21, 1.20));
    }

    [Fact]
    public void Large_Volume_And_Price_Normalization()
    {
        var props = CreateProps(minVolume: 0.1, tickSize: 0.0001);
        double vol = _calc.NormalizeVolume(props, 1000.123);
        Assert.Equal(1000.1, vol, 5);

        double price = _calc.NormalizePrice(props, 1.23456);
        Assert.Equal(1.2346, price, 4);
    }

    [Fact]
    public void PnL_Buy_Flat_Price_Returns_Zero()
    {
        var props = MakeProps();
        double pnl = _calc.CalculatePnL(props, 1.2000, 1.2000, 1.0, OrderType.Buy);
        Assert.Equal(0.0, pnl, 10);
    }

    [Fact]
    public void PnL_Sell_Flat_Price_Returns_Zero()
    {
        var props = MakeProps();
        double pnl = _calc.CalculatePnL(props, 1.2000, 1.2000, 1.0, OrderType.Sell);
        Assert.Equal(0.0, pnl, 10);
    }

    [Fact]
    public void PnL_Large_Movement()
    {
        var props = MakeProps();
        double pnl = _calc.CalculatePnL(props, 1.0000, 1.5000, 10.0, OrderType.Buy);
        Assert.True(pnl > 4000);
    }

    [Fact]
    public void Swap_Zero_Volume_Returns_Zero()
    {
        var props = MakeProps();
        double swap = _calc.CalculateSwap(props, 0.0, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(0.0, swap);
    }

    [Fact]
    public void Commission_Zero_Volume_Returns_Zero()
    {
        var props = MakeProps();
        double comm = _calc.CalculateCommission(props, 100, 0);
        Assert.Equal(0.0, comm);
    }

    [Fact]
    public void NormalizeVolume_Exactly_On_Step()
    {
        var props = new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
            MarginCurrency = "USD",
            ContractSize = 100000,
            TickSize = 0.0001,
            TickValue = 1,
            MinVolume = 0.1,
            MaxLeverage = 100,
            SwapLong = 0,
            SwapShort = 0,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = 0,
            InitialMarginRate = 0.02,
            MaintenanceMarginRate = 0.01,
            MakerFeeRate = 0.0005,
            TakerFeeRate = 0.001
        };
        Assert.Equal(1.0, _calc.NormalizeVolume(props, 1.0), 10);
        Assert.Equal(1.1, _calc.NormalizeVolume(props, 1.05), 10);
    }

    [Fact]
    public void NormalizePrice_Exactly_On_Tick()
    {
        var props = new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
            MarginCurrency = "USD",
            ContractSize = 100000,
            TickSize = 0.05,
            TickValue = 1,
            MinVolume = 0.01,
            MaxLeverage = 100,
            SwapLong = 0,
            SwapShort = 0,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = 0,
            InitialMarginRate = 0.02,
            MaintenanceMarginRate = 0.01,
            MakerFeeRate = 0.0005,
            TakerFeeRate = 0.001
        };
        Assert.Equal(1.25, _calc.NormalizePrice(props, 1.25), 10);
        Assert.Equal(1.25, _calc.NormalizePrice(props, 1.27), 10);
    }

    [Fact]
    public void IsPendingOrderTriggered_All_Combinations_UseMidPrice()
    {
        var props = MakeProps(PendingOrderTriggerMode.UseMidPrice);

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.0, 1.2, 1.2));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.0, 1.2, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.0, 1.2, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.0, 1.2, 1.2));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0.8, 1.0, 0.9));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0.8, 1.0, 1.0));

        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0.8, 1.0, 0.8));
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0.8, 1.0, 1.0));
    }

    [Fact]
    public void CalculateSwap_Sell_Short_Rate_Large_Volume()
    {
        var props = MakeProps();
        double swap = _calc.CalculateSwap(props, 5.0, OrderType.Sell, 0, TimeSpan.TicksPerDay * 2);
        Assert.Equal(-30.0, swap, 10);  // volume=5, rate=-3 per day, 2 days -> 5 * (-3) * 2 = -30
    }

    [Fact]
    public void CalculateFunding_LastFundingTime_Exactly_Current_Returns_Zero()
    {
        var props = MakeProps();
        double f = _calc.CalculateFunding(props, 1.0, 100.0, OrderType.Buy, TimeSpan.TicksPerHour, TimeSpan.TicksPerHour);
        Assert.Equal(0.0, f);
    }

    [Fact]
    public void CalculateFunding_Zero_Rate_Returns_Zero()
    {
        var props = new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
            MarginCurrency = "USD",
            ContractSize = 100000,
            TickSize = 0.0001,
            TickValue = 1,
            MinVolume = 0.01,
            MaxLeverage = 100,
            SwapLong = 0,
            SwapShort = 0,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = 0,
            InitialMarginRate = 0.02,
            MaintenanceMarginRate = 0.01,
            MakerFeeRate = 0.0005,
            TakerFeeRate = 0.001
        };
        double f = _calc.CalculateFunding(props, 1.0, 100.0, OrderType.Sell, TimeSpan.TicksPerHour, 0);
        Assert.Equal(0.0, f);
    }

    [Fact]
    public void IsPendingOrderTriggered_UseBidForBuy_All_Combinations()
    {
        var props = MakeProps(PendingOrderTriggerMode.UseBidForBuy);

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.20, 0, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.21, 0, 1.20));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.20, 0, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.19, 0, 1.20));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 1.20, 0, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 1.19, 0, 1.20));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.20, 0, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.21, 0, 1.20));
    }

    [Fact]
    public void IsPendingOrderTriggered_UseAskForBuy_Additional_Cases()
    {
        var props = MakeProps(PendingOrderTriggerMode.UseAskForBuy);

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.20, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.21, 1.20));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 0, 1.20, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 0, 1.19, 1.20));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0, 1.20, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0, 1.19, 1.20));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0, 1.20, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0, 1.21, 1.20));
    }

    [Fact]
    public void IsPendingOrderTriggered_Market_Buy_Returns_False()
    {
        var props = MakeProps();
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.Buy, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_Market_Sell_Returns_False()
    {
        var props = MakeProps();
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.Sell, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void CalculateSwap_Very_Small_Rate_Returns_Zero()
    {
        var props = MakeProps();
        // Set SwapLong to extremely small value so the Math.Abs check triggers zero
        var props2 = props with { SwapLong = 1e-13 };
        double swap = _calc.CalculateSwap(props2, 1.0, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(0.0, swap);
    }

    [Fact]
    public void CalculateFunding_Zero_FundingRate_Returns_Zero()
    {
        var props = MakeProps() with { FundingRate = 0 };
        double f = _calc.CalculateFunding(props, 1.0, 100.0, OrderType.Buy, TimeSpan.TicksPerHour, 0);
        Assert.Equal(0.0, f);
    }

    [Fact]
    public void CalculateFunding_LastFundingTime_Greater_Than_Current_Returns_Zero()
    {
        var props = MakeProps();
        double f = _calc.CalculateFunding(props, 1.0, 100.0, OrderType.Sell, TimeSpan.TicksPerHour, TimeSpan.TicksPerHour * 2);
        Assert.Equal(0.0, f);
    }

    [Fact]
    public void CalculateHoldingCost_Buy_Includes_Swap_And_Funding()
    {
        var props = MakeProps();
        double cost = _calc.CalculateHoldingCost(props, 2.0, 100.0, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        // Swap (2 * 5 * 1 = 10) + Funding (buy pays negative, large because of 100k contract size)
        // Result is negative because funding dominates
        Assert.True(cost < 0);
    }

    [Fact]
    public void CalculateSwap_Extremely_Small_Rate_Returns_Zero()
    {
        var props = MakeProps() with { SwapLong = 1e-13, SwapShort = 1e-13 };
        double swap = _calc.CalculateSwap(props, 1.0, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(0.0, swap);
    }

    [Fact]
    public void CalculateFunding_LastFundingTime_Exceeds_Current_Returns_Zero()
    {
        var props = MakeProps();
        double f = _calc.CalculateFunding(props, 1.0, 100.0, OrderType.Buy, TimeSpan.TicksPerHour, TimeSpan.TicksPerHour * 2);
        Assert.Equal(0.0, f);
    }

    [Fact]
    public void IsPendingOrderTriggered_Unknown_OrderType_Returns_False()
    {
        var props = MakeProps();
        Assert.False(_calc.IsPendingOrderTriggered(props, (OrderType)999, 1.0, 1.0, 1.0));
    }

    [Fact]
    public void CalculateSwap_Long_Rate_Near_Zero_Returns_Zero()
    {
        var props = MakeProps() with { SwapLong = 5e-14, SwapShort = 0 };
        double swap = _calc.CalculateSwap(props, 1.0, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(0.0, swap);
    }
}
