using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class SymbolPropertiesValidationTests
{
    private SymbolProperties CreateValid()
    {
        return new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Cross,
            PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
            MarginCurrency = "USD",
            ContractSize = 1,
            TickSize = 0.01,
            TickValue = 1,
            MinVolume = 0.01,
            MaxLeverage = 50,
            SwapLong = 0,
            SwapShort = 0,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 1,
            FundingRate = 0,
            InitialMarginRate = 1,
            MaintenanceMarginRate = 0.5,
            MakerFeeRate = 0.001,
            TakerFeeRate = 0.002
        };
    }

    [Fact]
    public void Validate_Valid_Properties_Does_Not_Throw()
    {
        var props = CreateValid();
        props.Validate();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_Invalid_MarginCurrency_Throws(string? currency)
    {
        var props = CreateValid() with
        {
            MarginCurrency = currency!
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ContractSize_Not_Positive_Throws(double contractSize)
    {
        var props = CreateValid() with
        {
            ContractSize = contractSize
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.001)]
    public void Validate_TickSize_Not_Positive_Throws(double tickSize)
    {
        var props = CreateValid() with
        {
            TickSize = tickSize
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_TickValue_Not_Positive_Throws(double tickValue)
    {
        var props = CreateValid() with
        {
            TickValue = tickValue
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Validate_MinVolume_Not_Positive_Throws(double minVolume)
    {
        var props = CreateValid() with
        {
            MinVolume = minVolume
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_MaxLeverage_Not_Positive_Throws(double maxLeverage)
    {
        var props = CreateValid() with
        {
            MaxLeverage = maxLeverage
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void Validate_SwapRolloverHourUtc_Out_Of_Range_Throws(int hour)
    {
        var props = CreateValid() with
        {
            SwapRolloverHourUtc = hour
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Fact]
    public void Validate_SwapRolloverHourUtc_Boundaries_Acceptable()
    {
        (CreateValid() with
        {
            SwapRolloverHourUtc = 0
        }).Validate();
        (CreateValid() with
        {
            SwapRolloverHourUtc = 23
        }).Validate();
    }

    [Fact]
    public void Validate_TripleSwapDayMultiplier_Negative_Throws()
    {
        var props = CreateValid() with
        {
            TripleSwapDayMultiplier = -0.1
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.1)]
    public void Validate_InitialMarginRate_Out_Of_Range_Throws(double rate)
    {
        var props = CreateValid() with
        {
            InitialMarginRate = rate
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.1)]
    public void Validate_MaintenanceMarginRate_Out_Of_Range_Throws(double rate)
    {
        var props = CreateValid() with
        {
            MaintenanceMarginRate = rate
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_MakerFeeRate_Out_Of_Range_Throws(double rate)
    {
        var props = CreateValid() with
        {
            MakerFeeRate = rate
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_TakerFeeRate_Out_Of_Range_Throws(double rate)
    {
        var props = CreateValid() with
        {
            TakerFeeRate = rate
        };
        Assert.Throws<ConfigurationException>(() => props.Validate());
    }

    [Fact]
    public void Validate_FeeRates_At_Boundaries_Acceptable()
    {
        (CreateValid() with
        {
            MakerFeeRate = 0
        }).Validate();
        (CreateValid() with
        {
            MakerFeeRate = 1
        }).Validate();
        (CreateValid() with
        {
            TakerFeeRate = 0
        }).Validate();
        (CreateValid() with
        {
            TakerFeeRate = 1
        }).Validate();
    }
}
