using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class EnumTests
{
    [Theory]
    [InlineData(ExecutionState.New, 0)]
    [InlineData(ExecutionState.PartiallyFilled, 1)]
    [InlineData(ExecutionState.Filled, 2)]
    [InlineData(ExecutionState.Canceled, 3)]
    [InlineData(ExecutionState.Rejected, 4)]
    [InlineData(ExecutionState.Expired, 5)]
    public void ExecutionState_Values(ExecutionState state, int expected) => Assert.Equal(expected, (int)state);

    [Theory]
    [InlineData(OrderType.Buy, 0)]
    [InlineData(OrderType.Sell, 1)]
    [InlineData(OrderType.BuyLimit, 2)]
    [InlineData(OrderType.SellLimit, 3)]
    [InlineData(OrderType.BuyStop, 4)]
    [InlineData(OrderType.SellStop, 5)]
    public void OrderType_Values(OrderType type, int expected) => Assert.Equal(expected, (int)type);

    [Fact]
    public void DataActionPolicy_Values()
    {
        Assert.Equal(0, (int)DataActionPolicy.KeepUntilExit);
        Assert.Equal(1, (int)DataActionPolicy.DeleteAfterTask);
        Assert.Equal(2, (int)DataActionPolicy.PersistentCache);
    }

    [Theory]
    [InlineData(TimeFrame.Tick, 0)]
    [InlineData(TimeFrame.M1, 1)]
    [InlineData(TimeFrame.M5, 5)]
    [InlineData(TimeFrame.M15, 15)]
    [InlineData(TimeFrame.M30, 30)]
    [InlineData(TimeFrame.H1, 60)]
    [InlineData(TimeFrame.H2, 120)]
    [InlineData(TimeFrame.H3, 180)]
    [InlineData(TimeFrame.H4, 240)]
    [InlineData(TimeFrame.H6, 360)]
    [InlineData(TimeFrame.H12, 720)]
    [InlineData(TimeFrame.D1, 1440)]
    [InlineData(TimeFrame.D2, 2880)]
    [InlineData(TimeFrame.D3, 4320)]
    [InlineData(TimeFrame.W1, 10080)]
    [InlineData(TimeFrame.MN1, 43200)]
    public void TimeFrame_Minute_Values(TimeFrame tf, int minutes) => Assert.Equal(minutes, (int)tf);

    [Theory]
    [InlineData(AssetClass.Forex, 0)]
    [InlineData(AssetClass.CryptoSpot, 1)]
    [InlineData(AssetClass.CryptoPerpetual, 2)]
    [InlineData(AssetClass.Equity, 3)]
    [InlineData(AssetClass.Future, 4)]
    [InlineData(AssetClass.CFD, 5)]
    public void AssetClass_Values(AssetClass ac, int expected) => Assert.Equal(expected, (int)ac);

    [Fact]
    public void MarginMode_Values()
    {
        Assert.Equal(0, (int)MarginMode.Cross);
        Assert.Equal(1, (int)MarginMode.Isolated);
    }

    [Fact]
    public void PendingOrderTriggerMode_Values()
    {
        Assert.Equal(0, (int)PendingOrderTriggerMode.UseBidForBuy);
        Assert.Equal(1, (int)PendingOrderTriggerMode.UseAskForBuy);
        Assert.Equal(2, (int)PendingOrderTriggerMode.UseMidPrice);
    }

    [Fact]
    public void PriceType_Values()
    {
        Assert.Equal(0, (int)PriceType.Bid);
        Assert.Equal(1, (int)PriceType.Ask);
        Assert.Equal(2, (int)PriceType.Mid);
    }

    [Fact]
    public void GeneType_Values()
    {
        Assert.Equal(0, (int)GeneType.Continuous);
        Assert.Equal(1, (int)GeneType.Discrete);
        Assert.Equal(2, (int)GeneType.Categorical);
        Assert.Equal(3, (int)GeneType.Structural);
        Assert.Equal(4, (int)GeneType.Parametric);
    }
}
