using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

internal sealed class TestableCalculator : DefaultMarketCalculator { }

public class DefaultMarketCalculatorTests
{
    private readonly TestableCalculator _calc = new();

    private SymbolProperties CreateProps(double tickSize = 0.01, double minVolume = 0.01, double contractSize = 1,
        double swapLong = 0, double swapShort = 0, double fundingRate = 0, double initialMarginRate = 1.0, double takerFeeRate = 0.002)
        => new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
            MarginCurrency = "USD",
            ContractSize = contractSize,
            TickSize = tickSize,
            TickValue = 1,
            MinVolume = minVolume,
            MaxLeverage = 50,
            SwapLong = swapLong,
            SwapShort = swapShort,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = fundingRate,
            InitialMarginRate = initialMarginRate,
            MaintenanceMarginRate = 0.5,
            MakerFeeRate = 0.001,
            TakerFeeRate = takerFeeRate
        };

    [Fact]
    public void NormalizeVolume_Rounds_To_MinVolume_Step()
    {
        var props = CreateProps(minVolume: 0.1);
        Assert.Equal(1.1, _calc.NormalizeVolume(props, 1.05), 10);
        Assert.Equal(1.1, _calc.NormalizeVolume(props, 1.06), 10);
    }

    [Fact]
    public void NormalizeVolume_MinVolume_Zero_Uses_Default_Step()
    {
        var props = CreateProps(minVolume: 0);
        Assert.Equal(1.0, _calc.NormalizeVolume(props, 1.0), 10);
    }

    [Fact]
    public void NormalizePrice_Rounds_To_TickSize()
    {
        var props = CreateProps(tickSize: 0.05);
        Assert.Equal(1.25, _calc.NormalizePrice(props, 1.27), 10);
    }

    [Fact]
    public void NormalizePrice_TickSize_Zero_Uses_Default()
    {
        var props = CreateProps(tickSize: 0);
        Assert.Equal(1.27, _calc.NormalizePrice(props, 1.27), 10);
    }

    [Fact]
    public void CalculateRequiredMargin_Basic()
    {
        var props = CreateProps(contractSize: 100000, initialMarginRate: 0.02);
        double margin = _calc.CalculateRequiredMargin(props, 1.2, 0.1, 10);
        Assert.Equal(24.0, margin, 10);
    }

    [Fact]
    public void CalculateRequiredMargin_Leverage_Zero_Treats_As_1()
    {
        var props = CreateProps(contractSize: 100000, initialMarginRate: 1);
        double margin = _calc.CalculateRequiredMargin(props, 1.2, 0.1, 0);
        Assert.Equal(12000.0, margin, 10);
    }

    [Fact]
    public void CalculatePnL_Buy_Rising_Price()
    {
        var props = CreateProps(tickSize: 0.01, contractSize: 100000);
        double pnl = _calc.CalculatePnL(props, 1.0, 1.1, 0.1, OrderType.Buy);
        Assert.Equal(1.0, pnl, 10);
    }

    [Fact]
    public void CalculatePnL_Sell_Falling_Price()
    {
        var props = CreateProps(tickSize: 0.01);
        double pnl = _calc.CalculatePnL(props, 1.1, 1.0, 0.1, OrderType.Sell);
        Assert.Equal(1.0, pnl, 10);
    }

    [Fact]
    public void CalculateCommission()
    {
        var props = CreateProps(takerFeeRate: 0.001);
        double comm = _calc.CalculateCommission(props, 100, 2);
        Assert.Equal(0.2, comm, 10);
    }

    [Fact]
    public void CalculateSwap_Zero_Rate_Returns_Zero()
    {
        var props = CreateProps(swapLong: 0, swapShort: 0);
        double swap = _calc.CalculateSwap(props, 1, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(0, swap);
    }

    [Fact]
    public void CalculateSwap_Buy_Long_Rate()
    {
        var props = CreateProps(swapLong: 5.0, swapShort: 0);
        double swap = _calc.CalculateSwap(props, 2, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(10.0, swap, 10);
    }

    [Fact]
    public void CalculateFunding_Zero_Rate_Returns_Zero()
    {
        var props = CreateProps(fundingRate: 0);
        double f = _calc.CalculateFunding(props, 1, 100, OrderType.Buy, TimeSpan.TicksPerHour, 0);
        Assert.Equal(0, f);
    }

    [Fact]
    public void CalculateFunding_LastFundingTime_Greater_Or_Equal_Current_Returns_Zero()
    {
        var props = CreateProps(fundingRate: 0.01);
        double f = _calc.CalculateFunding(props, 1, 100, OrderType.Buy, TimeSpan.TicksPerHour, TimeSpan.TicksPerHour);
        Assert.Equal(0, f);
    }

    [Fact]
    public void CalculateFunding_Buy_Pays_Negative()
    {
        var props = CreateProps(fundingRate: 0.01, contractSize: 1);
        double f = _calc.CalculateFunding(props, 1, 100, OrderType.Buy, TimeSpan.TicksPerHour, 0);
        Assert.Equal(-1.0, f, 10);
    }

    [Fact]
    public void CalculateFunding_Sell_Receives_Positive()
    {
        var props = CreateProps(fundingRate: 0.01, contractSize: 1);
        double f = _calc.CalculateFunding(props, 1, 100, OrderType.Sell, TimeSpan.TicksPerHour, 0);
        Assert.Equal(1.0, f, 10);
    }

    [Fact]
    public void IsPendingOrderTriggered_BuyLimit()
    {
        var props = CreateProps();
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.1, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_SellLimit()
    {
        var props = CreateProps();
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.0, 0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 0.9, 0, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_BuyStop()
    {
        var props = CreateProps();
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0, 0.9, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_SellStop()
    {
        var props = CreateProps();
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.0, 0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.1, 0, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_Unknown_Type_Returns_False()
    {
        var props = CreateProps();
        Assert.False(_calc.IsPendingOrderTriggered(props, (OrderType)999, 0, 0, 0));
    }

    [Fact]
    public void CalculateHoldingCost_Sum_Of_Swap_And_Funding()
    {
        var props = CreateProps(swapLong: 10, fundingRate: 0.05, contractSize: 1);
        double cost = _calc.CalculateHoldingCost(props, 2, 100, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.True(cost < 0);
    }

    [Fact]
    public void All_Methods_Throw_On_Null_Props()
    {
        Assert.Throws<ArgumentNullException>(() => _calc.NormalizeVolume(null!, 1));
        Assert.Throws<ArgumentNullException>(() => _calc.NormalizePrice(null!, 1));
        Assert.Throws<ArgumentNullException>(() => _calc.CalculateRequiredMargin(null!, 1, 1, 1));
        Assert.Throws<ArgumentNullException>(() => _calc.CalculatePnL(null!, 1, 1, 1, OrderType.Buy));
        Assert.Throws<ArgumentNullException>(() => _calc.CalculateCommission(null!, 1, 1));
        Assert.Throws<ArgumentNullException>(() => _calc.CalculateSwap(null!, 1, OrderType.Buy, 0, 1));
        Assert.Throws<ArgumentNullException>(() => _calc.CalculateFunding(null!, 1, 1, OrderType.Buy, 0, 1));
        Assert.Throws<ArgumentNullException>(() => _calc.IsPendingOrderTriggered(null!, OrderType.Buy, 0, 0, 0));
        Assert.Throws<ArgumentNullException>(() => _calc.CalculateHoldingCost(null!, 1, 1, OrderType.Buy, 0, 1));
    }
}
