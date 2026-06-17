using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class MathHelpersTests
{
    [Theory]
    [InlineData(5, 0, 10, 5)]
    [InlineData(-1, 0, 10, 0)]
    [InlineData(100, 0, 50, 50)]
    [InlineData(0.5, -1, 1, 0.5)]
    public void ClampToRange_Works(double value, double min, double max, double expected) =>
        Assert.Equal(expected, MathHelpers.ClampToRange(value, min, max));

    [Fact]
    public void Clamp_Min_Greater_Max_Does_Not_Fail() =>
        Assert.Equal(10.0, MathHelpers.ClampToRange(5, 10, 0));

    [Fact]
    public void CalculatePercentageChange_With_Peak_Zero_Returns_Zero() =>
        Assert.Equal(0.0, MathHelpers.CalculatePercentageChange(0, 100));

    [Theory]
    [InlineData(100, 90, 10.0)]
    [InlineData(200, 150, 25.0)]
    public void CalculatePercentageChange_Normal(double peak, double current, double expected) =>
        Assert.Equal(expected, MathHelpers.CalculatePercentageChange(peak, current));
}

public class TimeHelpersTests
{
    [Fact]
    public void FromUnixSeconds_Epoch() =>
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, TimeHelpers.FromUnixSeconds(0));

    [Fact]
    public void FromUnixMilliseconds_Epoch() =>
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, TimeHelpers.FromUnixMilliseconds(0));

    [Fact]
    public void Roundtrip_UnixSeconds_DateTime()
    {
        var dt = new DateTime(2025, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(dt, TimeHelpers.UnixToDateTime(TimeHelpers.DateTimeToUnix(dt)));
    }

    [Fact]
    public void FromUnixSeconds_Negative_Timestamp() =>
        Assert.True(TimeHelpers.FromUnixSeconds(-1) > 0);
}

public class ValidationHelpersTests
{
    [Fact]
    public void ValidatePositive_Throws_For_Zero_And_Negative()
    {
        Assert.Throws<ArgumentException>(() => ValidationHelpers.ValidatePositive(0, "x"));
        Assert.Throws<ArgumentException>(() => ValidationHelpers.ValidatePositive(-1, "x"));
        ValidationHelpers.ValidatePositive(0.001, "x");
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0.5, 0, 1)]
    [InlineData(1, 0, 1)]
    public void ValidateRange_Accepts_InRange(double v, double min, double max) =>
        ValidationHelpers.ValidateRange(v, min, max, "p");

    [Theory]
    [InlineData(-0.1, 0, 1)]
    [InlineData(1.1, 0, 1)]
    public void ValidateRange_Throws_OutOfRange(double v, double min, double max) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationHelpers.ValidateRange(v, min, max, "p"));

    [Fact]
    public void ValidateNotNull_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ValidationHelpers.ValidateNotNull<string>(null!, "x"));
        ValidationHelpers.ValidateNotNull("ok", "x");
    }
}
