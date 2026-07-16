using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class Helpers_IntegrationTests
{
    [Fact]
    public void ClampToRange_Large_Dataset()
    {
        for (double value = -1000; value <= 1000; value += 0.5)
        {
            double result = MathHelpers.ClampToRange(value, 0, 500);
            Assert.InRange(result, 0, 500);
        }
    }

    [Fact]
    public void CalculatePercentageChange_All_Scenarios()
    {
        Assert.Equal(50.0, MathHelpers.CalculatePercentageChange(200, 100));
        Assert.Equal(0.0, MathHelpers.CalculatePercentageChange(0, 100));
        Assert.Equal(0.0, MathHelpers.CalculatePercentageChange(100, 100));
        Assert.Equal(10.0, MathHelpers.CalculatePercentageChange(1000, 900));
    }

    [Fact]
    public void ValidatePositive_Large_Range()
    {
        ValidationHelpers.ValidatePositive(0.0001, "small");
        ValidationHelpers.ValidatePositive(double.MaxValue, "large");
        Assert.Throws<ArgumentException>(() => ValidationHelpers.ValidatePositive(-0.0001, "negative"));
    }

    [Fact]
    public void ValidateRange_Edge_Cases()
    {
        ValidationHelpers.ValidateRange(double.MinValue, double.MinValue, double.MaxValue, "wide");
        ValidationHelpers.ValidateRange(0.5, 0.5, 0.5, "exact");
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationHelpers.ValidateRange(0.49, 0.5, 0.5, "below"));
    }

    [Fact]
    public void TimeHelpers_Unix_Roundtrips()
    {
        var dt1 = new DateTime(2020, 3, 15, 8, 30, 0, DateTimeKind.Utc);
        long unix = TimeHelpers.DateTimeToUnix(dt1);
        Assert.Equal(dt1, TimeHelpers.UnixToDateTime(unix));

        long ticks1 = TimeHelpers.FromUnixSeconds(0);
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, ticks1);

        long ms = TimeHelpers.FromUnixMilliseconds(0);
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, ms);
    }
}
