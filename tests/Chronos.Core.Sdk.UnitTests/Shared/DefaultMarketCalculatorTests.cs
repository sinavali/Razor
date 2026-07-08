using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.UnitTests.Shared;

internal sealed class TestableCalculator : DefaultMarketCalculator
{
}

public class DefaultMarketCalculatorTests
{
    private readonly DefaultMarketCalculator _calc = new TestableCalculator();

    private SymbolProperties CreateProps(
        double tickSize = 0.01,
        double minVolume = 0.01,
        double contractSize = 1,
        double swapLong = 0,
        double swapShort = 0,
        double fundingRate = 0,
        double initialMarginRate = 1.0,
        double takerFeeRate = 0.002,
        PendingOrderTriggerMode triggerMode = PendingOrderTriggerMode.UseAskForBuy)
    {
        return new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = triggerMode,
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
    }

    // ── NormalizeVolume ──────────────────────────────────────────

    [Fact]
    public void NormalizeVolume_Rounds_To_MinVolume_Step()
    {
        var props = CreateProps(minVolume: 0.1);
        Assert.Equal(1.1, _calc.NormalizeVolume(props, 1.05), 10);
    }

    [Fact]
    public void NormalizeVolume_MinVolume_Zero_Throws()
    {
        Assert.Throws<ConfigurationException>(() => _calc.NormalizeVolume(CreateProps(minVolume: 0), 1));
    }

    [Fact]
    public void NormalizeVolume_Zero_Volume_Returns_Zero()
    {
        var props = CreateProps(minVolume: 0.1);
        Assert.Equal(0.0, _calc.NormalizeVolume(props, 0.0));
    }

    // ── NormalizePrice ───────────────────────────────────────────

    [Fact]
    public void NormalizePrice_Rounds_To_TickSize()
    {
        var props = CreateProps(tickSize: 0.05);
        Assert.Equal(1.25, _calc.NormalizePrice(props, 1.27), 10);
    }

    [Fact]
    public void NormalizePrice_TickSize_Zero_Throws()
    {
        Assert.Throws<ConfigurationException>(() => _calc.NormalizePrice(CreateProps(tickSize: 0), 1));
    }

    [Fact]
    public void NormalizePrice_Zero_Price_Returns_Zero()
    {
        var props = CreateProps(tickSize: 0.05);
        Assert.Equal(0.0, _calc.NormalizePrice(props, 0.0));
    }

    // ── CalculateRequiredMargin ──────────────────────────────────

    [Fact]
    public void CalculateRequiredMargin_Basic()
    {
        var props = CreateProps(contractSize: 100000, initialMarginRate: 0.02);
        Assert.Equal(24.0, _calc.CalculateRequiredMargin(props, 1.2, 0.1, 10), 10);
    }

    [Fact]
    public void CalculateRequiredMargin_Leverage_Zero_Treats_As_1()
    {
        var props = CreateProps(contractSize: 100000, initialMarginRate: 1);
        Assert.Equal(12000.0, _calc.CalculateRequiredMargin(props, 1.2, 0.1, 0), 10);
    }

    // ── CalculatePnL ─────────────────────────────────────────────

    [Fact]
    public void CalculatePnL_Buy_Rising_Price()
    {
        var props = CreateProps(tickSize: 0.01, contractSize: 100000);
        Assert.Equal(1.0, _calc.CalculatePnL(props, 1.0, 1.1, 0.1, OrderType.Buy), 10);
    }

    [Fact]
    public void CalculatePnL_Sell_Falling_Price()
    {
        var props = CreateProps(tickSize: 0.01);
        Assert.Equal(1.0, _calc.CalculatePnL(props, 1.1, 1.0, 0.1, OrderType.Sell), 10);
    }

    // ── CalculateCommission ──────────────────────────────────────

    [Fact]
    public void CalculateCommission()
    {
        Assert.Equal(0.2, _calc.CalculateCommission(CreateProps(takerFeeRate: 0.001), 100, 2), 10);
    }

    // ── CalculateSwap ────────────────────────────────────────────

    [Fact]
    public void CalculateSwap_Zero_Rate_Returns_Zero()
    {
        var props = CreateProps(swapLong: 0, swapShort: 0);
        Assert.Equal(0, _calc.CalculateSwap(props, 1, OrderType.Buy, 0, TimeSpan.TicksPerDay));
    }

    [Fact]
    public void CalculateSwap_Buy_Long_Rate()
    {
        var props = CreateProps(swapLong: 5.0, swapShort: 0);
        Assert.Equal(10.0, _calc.CalculateSwap(props, 2, OrderType.Buy, 0, TimeSpan.TicksPerDay), 10);
    }

    [Fact]
    public void CalculateSwap_Sell_Short_Rate()
    {
        var props = CreateProps(swapLong: 0, swapShort: 3.0);
        double swap = _calc.CalculateSwap(props, 2, OrderType.Sell, 0, TimeSpan.TicksPerDay);
        Assert.Equal(6.0, swap, 10);
    }

    [Fact]
    public void CalculateSwap_Very_Small_Rate_Returns_Zero()
    {
        var props = CreateProps(swapLong: 1e-13, swapShort: 0);
        double swap = _calc.CalculateSwap(props, 1, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        Assert.Equal(0.0, swap);
    }

    [Fact]
    public void CalculateSwap_Sell_Short_Rate_With_Fractional_Days()
    {
        var props = CreateProps(swapShort: 2.0);
        double swap = _calc.CalculateSwap(props, 3, OrderType.Sell, 0, TimeSpan.TicksPerHour * 12);
        Assert.Equal(3.0, swap, 10);
    }

    // ── CalculateFunding ─────────────────────────────────────────

    [Fact]
    public void CalculateFunding_Zero_Rate_Returns_Zero()
    {
        var props = CreateProps(fundingRate: 0);
        Assert.Equal(0, _calc.CalculateFunding(props, 1, 100, OrderType.Buy, TimeSpan.TicksPerHour, 0));
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
        Assert.Equal(-1.0, _calc.CalculateFunding(props, 1, 100, OrderType.Buy, TimeSpan.TicksPerHour, 0), 10);
    }

    [Fact]
    public void CalculateFunding_Sell_Receives_Positive()
    {
        var props = CreateProps(fundingRate: 0.01, contractSize: 1);
        Assert.Equal(1.0, _calc.CalculateFunding(props, 1, 100, OrderType.Sell, TimeSpan.TicksPerHour, 0), 10);
    }

    // ── IsPendingOrderTriggered ──────────────────────────────────

    [Fact]
    public void IsPendingOrderTriggered_UseAskForBuy_All_Types()
    {
        var props = CreateProps(triggerMode: PendingOrderTriggerMode.UseAskForBuy);

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.1, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 0, 0.9, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0, 0.9, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0, 1.1, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_UseBidForBuy_All_Types()
    {
        var props = CreateProps(triggerMode: PendingOrderTriggerMode.UseBidForBuy);

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.0, 0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.1, 0, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.0, 0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 0.9, 0, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 1.0, 0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 0.9, 0, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.0, 0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.1, 0, 1.0));
    }

    [Fact]
    public void IsPendingOrderTriggered_UseMidPrice_All_Types()
    {
        var props = CreateProps(triggerMode: PendingOrderTriggerMode.UseMidPrice);

        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 1.0, 1.2, 1.0));
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0.8, 1.2, 1.0));
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 2.0, 1.0));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.0, 1.2, 1.1));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellLimit, 1.0, 1.2, 1.2));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 1.0, 1.2, 1.1));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.BuyStop, 1.0, 1.2, 1.2));

        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 0.8, 1.2, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.0, 1.2, 0.9));
    }

    [Fact]
    public void IsPendingOrderTriggered_Unknown_Type_Returns_False()
    {
        var props = CreateProps();
        Assert.False(_calc.IsPendingOrderTriggered(props, (OrderType)999, 0, 0, 0));
    }

    [Fact]
    public void IsPendingOrderTriggered_Market_Orders_Return_False()
    {
        var props = CreateProps();
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.Buy, 1.0, 1.0, 1.0));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.Sell, 1.0, 1.0, 1.0));
    }

    // ── CalculateHoldingCost ─────────────────────────────────────

    [Fact]
    public void CalculateHoldingCost_Sum_Of_Swap_And_Funding()
    {
        var props = CreateProps(swapLong: 10, fundingRate: 0.05, contractSize: 1);
        double cost = _calc.CalculateHoldingCost(props, 2, 100, OrderType.Buy, 0, TimeSpan.TicksPerDay);
        // Holding cost = swap (2 * 10 * 1 = 20) + funding for one day (24 hours)
        // Funding: openPrice*volume*contractSize = 100*2*1 = 200, fundingRate=0.05, 24 hours = 24 periods
        // Buy pays negative: -200 * 0.05 * 24 = -240
        // Total = 20 + (-240) = -220
        Assert.Equal(-220.0, cost, 10);
    }

    // ── Null Guards ──────────────────────────────────────────────

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

    // ── Additional PnL / Swap / Funding ─────────────────────────

    [Fact]
    public void CalculatePnL_Buy_Falling_Price_Negative_PnL()
    {
        var props = CreateProps(tickSize: 0.01, contractSize: 100000);
        double pnl = _calc.CalculatePnL(props, 1.1, 1.0, 0.1, OrderType.Buy);
        Assert.Equal(-1.0, pnl, 10);
    }

    [Fact]
    public void CalculatePnL_Sell_Rising_Price_Negative_PnL()
    {
        var props = CreateProps(tickSize: 0.01);
        double pnl = _calc.CalculatePnL(props, 1.0, 1.1, 0.1, OrderType.Sell);
        Assert.Equal(-1.0, pnl, 10);
    }

    [Fact]
    public void CalculateSwap_Long_Position_With_OpenTime_And_CloseTime()
    {
        var props = CreateProps(swapLong: 5.0, swapShort: 0);
        double swap = _calc.CalculateSwap(props, 2, OrderType.Buy, TimeSpan.TicksPerHour, TimeSpan.TicksPerHour * 25);
        Assert.Equal(10.0, swap, 10);
    }

    [Fact]
    public void CalculateFunding_With_Multiple_Periods()
    {
        var props = CreateProps(fundingRate: 0.01, contractSize: 1);
        double f = _calc.CalculateFunding(props, 1, 100, OrderType.Buy, TimeSpan.TicksPerHour * 3, 0);
        Assert.Equal(-3.0, f, 10);
    }
}
