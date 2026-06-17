using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class SymbolProperties_IntegrationTests
{
    private static SymbolProperties CreateValid()
    {
        return new SymbolProperties
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
            SwapLong = 1.5,
            SwapShort = -1.0,
            SwapRolloverHourUtc = 12,
            TripleSwapDayMultiplier = 1,
            FundingRate = 0.0001,
            InitialMarginRate = 0.02,
            MaintenanceMarginRate = 0.01,
            MakerFeeRate = 0.0005,
            TakerFeeRate = 0.001
        };
    }

    [Fact]
    public void Create_1000_SymbolProperties_And_Validate()
    {
        for (int i = 0; i < 1000; i++)
        {
            var props = new SymbolProperties
            {
                AssetClass = AssetClass.Forex,
                MarginMode = MarginMode.Cross,
                PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
                MarginCurrency = "USD",
                ContractSize = 100_000 + i,
                TickSize = 0.0001,
                TickValue = 1,
                MinVolume = 0.01,
                MaxLeverage = 100 + i % 900,
                SwapLong = 1.5,
                SwapShort = -1.0,
                SwapRolloverHourUtc = i % 24,
                TripleSwapDayMultiplier = 1,
                FundingRate = 0.0001,
                InitialMarginRate = 0.02,
                MaintenanceMarginRate = 0.01,
                MakerFeeRate = 0.0005,
                TakerFeeRate = 0.001
            };
            props.Validate();
            Assert.Equal("USD", props.MarginCurrency);
        }
    }

    [Fact]
    public void Validation_Catches_All_Errors()
    {
        var valid = CreateValid();
        valid.Validate();

        Assert.Throws<ConfigurationException>(() => (valid with { MarginCurrency = "" }).Validate());
        Assert.Throws<ConfigurationException>(() => (valid with { ContractSize = -1 }).Validate());
        Assert.Throws<ConfigurationException>(() => (valid with { TickSize = 0 }).Validate());
        Assert.Throws<ConfigurationException>(() => (valid with { TickValue = -5 }).Validate());
        Assert.Throws<ConfigurationException>(() => (valid with { MaxLeverage = 0 }).Validate());
        Assert.Throws<ConfigurationException>(() => (valid with { InitialMarginRate = 1.5 }).Validate());
        Assert.Throws<ConfigurationException>(() => (valid with { MaintenanceMarginRate = -0.1 }).Validate());
    }

    [Fact]
    public void Validate_ContractSize_Zero_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { ContractSize = 0 }).Validate());
    }

    [Fact]
    public void Validate_TickSize_Zero_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { TickSize = 0 }).Validate());
    }

    [Fact]
    public void Validate_MinVolume_Zero_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { MinVolume = 0 }).Validate());
    }

    [Fact]
    public void Validate_MakerFeeRate_Negative_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { MakerFeeRate = -0.1 }).Validate());
    }

    [Fact]
    public void Validate_TakerFeeRate_Greater_Than_One_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { TakerFeeRate = 1.5 }).Validate());
    }

    [Fact]
    public void Validate_SwapRolloverHourUtc_Negative_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { SwapRolloverHourUtc = -1 }).Validate());
    }

    [Fact]
    public void Validate_SwapRolloverHourUtc_OutOfRange_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { SwapRolloverHourUtc = 24 }).Validate());
    }

    [Fact]
    public void Validate_TripleSwapDayMultiplier_Negative_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { TripleSwapDayMultiplier = -0.01 }).Validate());
    }

    [Fact]
    public void Validate_InitialMarginRate_Zero_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { InitialMarginRate = 0 }).Validate());
    }

    [Fact]
    public void Validate_MaintenanceMarginRate_Zero_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { MaintenanceMarginRate = 0 }).Validate());
    }

    [Fact]
    public void Validate_MakerFeeRate_Above_One_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { MakerFeeRate = 1.1 }).Validate());
    }

    [Fact]
    public void Validate_TakerFeeRate_Negative_Throws()
    {
        var props = CreateValid();
        Assert.Throws<ConfigurationException>(() => (props with { TakerFeeRate = -0.001 }).Validate());
    }
}
