using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class MathHelpersTests
{
    [Theory]
    [InlineData(5, 0, 10, 5)]
    [InlineData(-1, 0, 10, 0)]
    [InlineData(100, 0, 50, 50)]
    [InlineData(0.5, -1, 1, 0.5)]
    public void ClampToRange_Works(double value, double min, double max, double expected)
    {
        Assert.Equal(expected, MathHelpers.ClampToRange(value, min, max));
    }

    [Fact]
    public void Clamp_Min_Greater_Max_Does_Not_Fail()
    {
        // Implementation uses Math.Max(min, Math.Min(max, value)).
        // For min=10, max=0, value=5: Math.Min(0,5)=0, Math.Max(10,0)=10.
        double result = MathHelpers.ClampToRange(5, 10, 0);
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void CalculatePercentageChange_With_Peak_Zero_Returns_Zero()
    {
        Assert.Equal(0.0, MathHelpers.CalculatePercentageChange(0, 100));
    }

    [Theory]
    [InlineData(100, 90, 10.0)]
    [InlineData(200, 150, 25.0)]
    public void CalculatePercentageChange_Normal(double peak, double current, double expected)
    {
        Assert.Equal(expected, MathHelpers.CalculatePercentageChange(peak, current));
    }
}

public class TimeHelpersTests
{
    [Fact]
    public void FromUnixSeconds_Epoch()
    {
        long ticks = TimeHelpers.FromUnixSeconds(0);
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, ticks);
    }

    [Fact]
    public void FromUnixMilliseconds_Epoch()
    {
        long ticks = TimeHelpers.FromUnixMilliseconds(0);
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, ticks);
    }

    [Fact]
    public void Roundtrip_UnixSeconds_DateTime()
    {
        var dt = new DateTime(2025, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        long unix = TimeHelpers.DateTimeToUnix(dt);
        Assert.Equal(dt, TimeHelpers.UnixToDateTime(unix));
    }

    [Fact]
    public void FromUnixSeconds_Negative_Timestamp()
    {
        long ticks = TimeHelpers.FromUnixSeconds(-1);
        // Unix -1 corresponds to 1969-12-31 23:59:59, which is before 1970 but the .NET ticks value is still positive
        // (the number of 100-ns intervals since 1/1/0001). Just verify it doesn't throw.
        Assert.True(ticks > 0);
    }
}

public class ValidationHelpersTests
{
    [Fact]
    public void ValidatePositive_Throws_For_Zero_And_Negative()
    {
        Assert.Throws<ArgumentException>(() => ValidationHelpers.ValidatePositive(0, "x"));
        Assert.Throws<ArgumentException>(() => ValidationHelpers.ValidatePositive(-1, "x"));
        ValidationHelpers.ValidatePositive(0.001, "x"); // no throw
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0.5, 0, 1)]
    [InlineData(1, 0, 1)]
    public void ValidateRange_Accepts_InRange(double v, double min, double max)
    {
        ValidationHelpers.ValidateRange(v, min, max, "p");
        Assert.True(true);
    }

    [Theory]
    [InlineData(-0.1, 0, 1)]
    [InlineData(1.1, 0, 1)]
    public void ValidateRange_Throws_OutOfRange(double v, double min, double max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationHelpers.ValidateRange(v, min, max, "p"));
    }

    [Fact]
    public void ValidateNotNull_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ValidationHelpers.ValidateNotNull<string>(null!, "x"));
        ValidationHelpers.ValidateNotNull("ok", "x");
    }
}

public class ExecutionHelpersTests
{
    [Fact]
    public void ResolveParallelism_Positive_Returns_Value()
    {
        Assert.Equal(4, ExecutionHelpers.ResolveParallelism(4));
    }

    [Fact]
    public void ResolveParallelism_Zero_Returns_ProcessorCount()
    {
        Assert.Equal(Environment.ProcessorCount, ExecutionHelpers.ResolveParallelism(0));
    }

    [Fact]
    public void ResolveParallelism_Negative_Returns_ProcessorCount()
    {
        // Implementation: configuredThreads > 0 ? configuredThreads : Environment.ProcessorCount
        Assert.Equal(Environment.ProcessorCount, ExecutionHelpers.ResolveParallelism(-1));
    }
}
