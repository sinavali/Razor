using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Strategies;

internal sealed class TestableCalculator : DefaultMarketCalculator { }

public class DefaultMarketCalculator_IntegrationTests
{
    private readonly TestableCalculator _calc = new();

    private SymbolProperties CreateProps(double tickSize = 0.01, double minVolume = 0.01, double contractSize = 100_000,
        double initialMarginRate = 0.02, double takerFee = 0.002, double swapLong = 10, double fundingRate = 0.001)
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

    [Fact]
    public void Full_Calculation_Pipeline()
    {
        var props = CreateProps();

        // Normalize
        double volume = _calc.NormalizeVolume(props, 0.12);
        double price = _calc.NormalizePrice(props, 1.23456);
        Assert.Equal(0.12, volume, 5);
        Assert.Equal(1.23, price, 2);

        // Margin
        double margin = _calc.CalculateRequiredMargin(props, price, volume, 10);
        Assert.True(margin > 0);

        // PnL for a buy
        double pnl = _calc.CalculatePnL(props, price, price + 0.01, volume, OrderType.Buy);
        Assert.True(pnl > 0);

        // Commission
        double comm = _calc.CalculateCommission(props, price, volume);
        Assert.True(comm >= 0);

        // Swap
        long oneDay = TimeSpan.TicksPerDay;
        double swap = _calc.CalculateSwap(props, volume, OrderType.Buy, 0, oneDay);
        Assert.Equal(volume * 10, swap); // long swap rate = 10 per day

        // Funding
        double funding = _calc.CalculateFunding(props, volume, price, OrderType.Sell, oneDay, 0);
        Assert.True(funding > 0); // sell receives positive

        // Holding cost
        double holding = _calc.CalculateHoldingCost(props, volume, price, OrderType.Buy, 0, oneDay);
        Assert.True(holding < 0); // buy pays swap and funding?

        // Pending trigger checks
        Assert.True(_calc.IsPendingOrderTriggered(props, OrderType.BuyLimit, 0, 1.20, 1.20));
        Assert.False(_calc.IsPendingOrderTriggered(props, OrderType.SellStop, 1.21, 0, 1.20));
    }
}
