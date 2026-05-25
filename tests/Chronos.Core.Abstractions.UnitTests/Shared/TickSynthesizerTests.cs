using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class TickSynthesizerTests
{
    [Fact]
    public void BarsToTicks_Empty_Array_Returns_Empty()
    {
        var ticks = TickSynthesizer.BarsToTicks(Array.Empty<Bar>());
        Assert.Empty(ticks);
    }

    [Fact]
    public void BarsToTicks_Null_Array_Returns_Empty()
    {
        var ticks = TickSynthesizer.BarsToTicks(null!);
        Assert.Empty(ticks);
    }

    [Fact]
    public void BarsToTicks_Single_Bar_Produces_4_Ticks()
    {
        var bar = new Bar(100, 1.0, 1.5, 0.9, 1.2, 400, 200);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar });
        Assert.Equal(4, ticks.Length);
        Assert.Equal(bar.OpenTime, ticks[0].Time);
        Assert.Equal(bar.Open, ticks[0].Bid);
        Assert.Equal(bar.Open, ticks[0].Ask);
        Assert.Equal(100.0, ticks[0].Volume); // 400*0.25
        Assert.True(ticks[0].IsSynthetic);
        Assert.Equal(bar.Close, ticks[3].Bid);
    }

    [Fact]
    public void BarsToTicks_Multiple_Bars()
    {
        var bars = new Bar[]
        {
            new Bar(0, 10, 12, 8, 11, 1000, 100),
            new Bar(200, 11, 13, 10, 12, 800, 300)
        };
        var ticks = TickSynthesizer.BarsToTicks(bars);
        Assert.Equal(8, ticks.Length);
    }

    [Fact]
    public void BarsToTicks_Bar_With_Zero_Or_Negative_Duration()
    {
        var bar = new Bar(100, 1, 2, 0.5, 1.5, 0, 100); // duration=0
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar });
        Assert.Equal(4, ticks.Length);
        // Should not throw
    }

    [Fact]
    public void Randomized_BarsToTicks_Empty_Returns_Empty()
    {
        var ticks = TickSynthesizer.BarsToTicks(Array.Empty<Bar>(), 5, 123);
        Assert.Empty(ticks);
    }

    [Fact]
    public void Randomized_BarsToTicks_TicksPerBar_LessThan_2_Clamped_To_2()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 100);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar }, 1, 123);
        Assert.Equal(2, ticks.Length);
    }

    [Fact]
    public void Randomized_BarsToTicks_Deterministic_With_Same_Seed()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 100);
        var a = TickSynthesizer.BarsToTicks(new[] { bar }, 5, 42);
        var b = TickSynthesizer.BarsToTicks(new[] { bar }, 5, 42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Randomized_BarsToTicks_Prices_Within_Range()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 100);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar }, 10, 123);
        for (int i = 1; i < ticks.Length - 1; i++)
        {
            Assert.InRange(ticks[i].Bid, bar.Low, bar.High);
        }
    }

    [Fact]
    public void ComputeStreamMetrics_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TickSynthesizer.ComputeStreamMetrics(null!));
    }

    [Fact]
    public void ComputeStreamMetrics_Empty_Streams_Returns_Defaults()
    {
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(Array.Empty<IReadOnlyList<Tick>>());
        Assert.Equal(5000, est);
        Assert.Equal(0, max);
    }

    [Fact]
    public void ComputeStreamMetrics_With_Single_Stream()
    {
        var ticks = new Tick[] { new Tick(0, 1, 2, 0), new Tick(TimeSpan.TicksPerSecond * 10, 3, 4, 0) };
        var streams = new IReadOnlyList<Tick>[] { ticks };
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(streams);
        Assert.Equal(10, est); // (max-min)/TicksPerSecond = 10
        Assert.Equal(2, max);
    }

    [Fact]
    public void ComputeStreamMetrics_Null_Elements_Skipped()
    {
        var ticks = new Tick[] { new Tick(0, 1, 2, 0) };
        var streams = new IReadOnlyList<Tick>[] { ticks, null! };
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(streams);
        Assert.Equal(1, max);
    }

    [Fact]
    public void ComputeStreamMetrics_All_Empty_Streams()
    {
        var streams = new IReadOnlyList<Tick>[] { Array.Empty<Tick>(), Array.Empty<Tick>() };
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(streams);
        Assert.Equal(5000, est);
        Assert.Equal(0, max);
    }

    [Fact]
    public void ComputeStreamMetrics_MaxTicks_Uses_Max_Count()
    {
        var s1 = new Tick[5];
        var s2 = new Tick[3];
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { s1, s2 });
        Assert.Equal(5, max);
    }

    [Fact]
    public void ComputeStreamMetrics_EstimatedTicks_Capped_At_IntMax()
    {
        var s = new Tick[] { new Tick(0, 1, 2, 0), new Tick(long.MaxValue, 3, 4, 0) };
        var (est, _) = TickSynthesizer.ComputeStreamMetrics(new[] { s });
        Assert.Equal(int.MaxValue, est);
    }
}
