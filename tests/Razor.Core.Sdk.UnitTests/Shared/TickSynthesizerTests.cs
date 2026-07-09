using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class TickSynthesizerTests
{
    // ── Basic BarsToTicks ────────────────────────────────────────

    [Fact]
    public void BarsToTicks_Empty_Array_Returns_Empty()
    {
        Assert.Empty(TickSynthesizer.BarsToTicks(Array.Empty<Bar>()));
    }

    [Fact]
    public void BarsToTicks_Null_Array_Returns_Empty()
    {
        Assert.Empty(TickSynthesizer.BarsToTicks(null!));
    }

    [Fact]
    public void BarsToTicks_Single_Bar_Produces_4_Ticks()
    {
        var bar = new Bar(100, 1.0, 1.5, 0.9, 1.2, 400, 200);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar });
        Assert.Equal(4, ticks.Length);
        Assert.Equal(bar.OpenTime, ticks[0].Time);
        Assert.Equal(bar.Open, ticks[0].Bid);
        Assert.Equal(100.0, ticks[0].Volume);
        Assert.True(ticks[0].IsSynthetic);
        Assert.Equal(bar.Close, ticks[3].Bid);
    }

    [Fact]
    public void BarsToTicks_Multiple_Bars()
    {
        Assert.Equal(8, TickSynthesizer.BarsToTicks(new Bar[]
        {
            new(0, 10, 12, 8, 11, 1000, 100),
            new(200, 11, 13, 10, 12, 800, 300)
        }).Length);
    }

    [Fact]
    public void BarsToTicks_Bar_With_Zero_Duration_Works()
    {
        Assert.Equal(4, TickSynthesizer.BarsToTicks(new[] { new Bar(100, 1, 2, 0.5, 1.5, 0, 100) }).Length);
    }

    [Fact]
    public void BarsToTicks_Bearish_Bar_Produces_Reverse_Ticks()
    {
        var bar = new Bar(100, 1.5, 1.8, 1.2, 1.0, 400, 200);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar });
        Assert.Equal(4, ticks.Length);
        Assert.Equal(bar.Open, ticks[0].Bid);
        Assert.Equal(bar.High, ticks[1].Bid);
        Assert.Equal(bar.Low, ticks[2].Bid);
        Assert.Equal(bar.Close, ticks[3].Bid);
    }

    [Fact]
    public void BarsToTicks_Bar_With_Same_Open_Close_Price_Produces_Correct_Order()
    {
        var bar = new Bar(0, 1.0, 2.0, 0.5, 1.0, 100, 100);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar });
        Assert.Equal(4, ticks.Length);
        Assert.Equal(1.0, ticks[0].Bid);
        Assert.Equal(0.5, ticks[1].Bid);
        Assert.Equal(2.0, ticks[2].Bid);
        Assert.Equal(1.0, ticks[3].Bid);
    }

    // ── Randomized BarsToTicks ───────────────────────────────────

    [Fact]
    public void Randomized_BarsToTicks_Empty_Returns_Empty()
    {
        Assert.Empty(TickSynthesizer.BarsToTicks(Array.Empty<Bar>(), 5, 123));
    }

    [Fact]
    public void Randomized_BarsToTicks_TicksPerBar_LessThan_2_Clamped_To_2()
    {
        Assert.Equal(2, TickSynthesizer.BarsToTicks(new[] { new Bar(0, 10, 12, 8, 11, 1000, 100) }, 1, 123).Length);
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
    public void Randomized_BarsToTicks_Negative_Seed_Throws()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => TickSynthesizer.BarsToTicks(new[] { bar }, 5, -1));
    }

    [Fact]
    public void Randomized_BarsToTicks_TickTime_Cannot_Exceed_CloseTime()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 10);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar }, 3, 42);
        Assert.Equal(3, ticks.Length);
        for (int i = 0; i < ticks.Length - 1; i++)
        {
            Assert.True(ticks[i].Time <= bar.CloseTime);
        }
    }

    // ── ComputeStreamMetrics ─────────────────────────────────────

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
        var ticks = new Tick[] { new(0, 1, 2, 0), new(TimeSpan.TicksPerSecond * 10, 3, 4, 0) };
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { ticks });
        Assert.Equal(10, est);
        Assert.Equal(2, max);
    }

    [Fact]
    public void ComputeStreamMetrics_Null_Elements_Skipped()
    {
        var ticks = new Tick[] { new(0, 1, 2, 0) };
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { ticks, null! });
        Assert.Equal(1, max);
    }

    [Fact]
    public void ComputeStreamMetrics_All_Streams_Null_Returns_Defaults()
    {
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { null!, null! });
        Assert.Equal(5000, est);
        Assert.Equal(0, max);
    }

    [Fact]
    public void ComputeStreamMetrics_Stream_With_No_Ticks_Skipped()
    {
        var streams = new IReadOnlyList<Tick>[] { Array.Empty<Tick>(), new Tick[] { new(100, 1, 2, 0) } };
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(streams);
        Assert.Equal(1, max);
    }

    [Fact]
    public void ComputeStreamMetrics_MaxTicks_Uses_Max_Count()
    {
        Assert.Equal(5, TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { new Tick[5], new Tick[3] }).maxTicks);
    }

    [Fact]
    public void ComputeStreamMetrics_EstimatedTicks_Capped_At_IntMax()
    {
        var s = new Tick[] { new(0, 1, 2, 0), new(long.MaxValue, 3, 4, 0) };
        Assert.Equal(int.MaxValue, TickSynthesizer.ComputeStreamMetrics(new[] { s }).estimatedTicks);
    }

    [Fact]
    public void ComputeStreamMetrics_Negative_Timestamp_Difference_Returns_Estimate()
    {
        var ticks = new Tick[] { new(long.MaxValue, 1, 2, 0), new(0, 3, 4, 0) };
        var (est, _) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { ticks });
        Assert.True(est >= 0);
    }

    // ── Additional branch coverage ──────────────────────────────

    [Fact]
    public void Randomized_BarsToTicks_TickTime_Clamped_To_CloseTime_Minus_One()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 3);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar }, 5, 42);
        Assert.Equal(bar.CloseTime - 1, ticks[^1].Time);
    }

    [Fact]
    public void Randomized_BarsToTicks_Price_Clamped_To_Low()
    {
        var bar = new Bar(0, 10, 12, 8, 11, 1000, 100);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar }, 100, 999999);
        for (int i = 1; i < ticks.Length - 1; i++)
        {
            Assert.True(ticks[i].Bid >= bar.Low);
            Assert.True(ticks[i].Bid <= bar.High);
        }
    }

    [Fact]
    public void Randomized_BarsToTicks_Null_Bars_Returns_Empty()
    {
        var ticks = TickSynthesizer.BarsToTicks(null!, 5, 42);
        Assert.Empty(ticks);
    }

    [Fact]
    public void ComputeStreamMetrics_Single_Stream_With_One_Tick()
    {
        var ticks = new Tick[] { new(100, 1, 2, 0) };
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { ticks });
        Assert.Equal(1, max);
    }

    [Fact]
    public void ComputeStreamMetrics_Multiple_Streams_With_Null_Member()
    {
        var s1 = new Tick[] { new(100, 1, 2, 0), new(200, 3, 4, 0) };
        var s2 = new Tick[] { new(150, 5, 6, 0) };
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { s1, null!, s2 });
        Assert.Equal(2, max);
    }

    [Fact]
    public void ComputeStreamMetrics_All_Empty_Streams()
    {
        var streams = new IReadOnlyList<Tick>[] { Array.Empty<Tick>(), Array.Empty<Tick>() };
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(streams);
        Assert.Equal(5000, est);
        Assert.Equal(0, max);
    }
}
