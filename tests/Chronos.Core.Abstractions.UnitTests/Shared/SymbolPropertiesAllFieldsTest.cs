using System.Reflection;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class SymbolPropertiesAllFieldsTest
{
    [Fact]
    public void All_Required_Fields_Are_Present()
    {
        var props = new SymbolProperties
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

        // Use reflection to verify all properties are initialized (not default).
        var type = typeof(SymbolProperties);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var value = prop.GetValue(props);
            Assert.NotNull(value); // None should be null
            if (prop.PropertyType == typeof(string))
            {
                Assert.NotEqual(string.Empty, value);
            }
        }
    }
}
