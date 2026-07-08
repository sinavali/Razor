using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.IntegrationTests.Shared;

public class TickSynthesizer_IntegrationTests
{
    [Fact]
    public void Convert_One_Thousand_Bars_To_Ticks_Deterministic()
    {
        var bars = new Bar[1000];
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = new Bar(i * 60L, 10 + i * 0.01, 15 + i * 0.01, 9 + i * 0.01, 12 + i * 0.01, 1000 + i, (i + 1) * 60L - 1);
        }

        var ticks1 = TickSynthesizer.BarsToTicks(bars);
        var ticks2 = TickSynthesizer.BarsToTicks(bars);
        Assert.Equal(ticks1, ticks2);

        Assert.Equal(4000, ticks1.Length);
        Assert.All(ticks1, t => Assert.True(t.IsSynthetic));
    }

    [Fact]
    public void Randomized_Ticks_Deterministic_With_Same_Seed()
    {
        var bars = new Bar[100];
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = new Bar(i * 60L, 100, 110, 90, 105, 500, (i + 1) * 60L - 1);
        }

        var ticksA = TickSynthesizer.BarsToTicks(bars, 10, 42);
        var ticksB = TickSynthesizer.BarsToTicks(bars, 10, 42);
        Assert.Equal(ticksA, ticksB);
    }

    [Fact]
    public void ComputeStreamMetrics_With_Large_Streams()
    {
        var s1 = new Tick[10000];
        var s2 = new Tick[5000];
        for (int i = 0; i < s1.Length; i++)
        {
            s1[i] = new Tick(i * TimeSpan.TicksPerSecond, 1, 2, 0);
        }

        for (int i = 0; i < s2.Length; i++)
        {
            s2[i] = new Tick(i * TimeSpan.TicksPerSecond * 2, 3, 4, 0);
        }

        var (est, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { s1, s2 });
        Assert.Equal(10000, max);
        Assert.True(est > 0);
    }

    [Fact]
    public void Convert_10000_Bars_Randomized_Deterministic()
    {
        var bars = new Bar[10000];
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = new Bar(i * 60L, 100, 110, 90, 105, 500, (i + 1) * 60L - 1);
        }

        var a = TickSynthesizer.BarsToTicks(bars, 5, 12345);
        var b = TickSynthesizer.BarsToTicks(bars, 5, 12345);
        Assert.Equal(a, b);
        Assert.Equal(50000, a.Length);
    }

    [Fact]
    public void ComputeStreamMetrics_Mixed_Empty_And_Large()
    {
        var large = new Tick[100000];
        for (int i = 0; i < large.Length; i++)
        {
            large[i] = new Tick(i * TimeSpan.TicksPerSecond, 1, 2, 0);
        }

        var (est, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { Array.Empty<Tick>(), large, null!, Array.Empty<Tick>() });
        Assert.Equal(100000, max);
        Assert.True(est > 0);
    }

    [Fact]
    public void ComputeStreamMetrics_Single_Stream_With_Same_Timestamps()
    {
        var ticks = new Tick[] { new(1000, 1, 2, 0), new(1000, 3, 4, 0), new(2000, 5, 6, 0) };
        var (_, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { ticks });
        Assert.Equal(3, max);
    }

    [Fact]
    public void BarsToTicks_Negative_Duration()
    {
        var bar = new Bar(200, 10, 12, 8, 11, 100, 100);
        var ticks = TickSynthesizer.BarsToTicks(new[] { bar });
        Assert.Equal(4, ticks.Length);
    }

    [Fact]
    public void Bar_Equality_Checks()
    {
        var b1 = new Bar(100, 1.0, 2.0, 0.5, 1.5, 500, 200);
        var b2 = new Bar(100, 1.0, 2.0, 0.5, 1.5, 500, 200);
        var b3 = new Bar(200, 1.0, 2.0, 0.5, 1.5, 500, 200);

        Assert.True(b1 == b2);
        Assert.False(b1 == b3);
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
        Assert.NotEqual(b1.GetHashCode(), b3.GetHashCode());
    }

    [Fact]
    public void Randomized_BarsToTicks_Negative_Seed_Throws()
    {
        var bars = new Bar[] { new(0, 100, 110, 90, 105, 500, 60) };
        Assert.Throws<ArgumentOutOfRangeException>(() => TickSynthesizer.BarsToTicks(bars, 5, -1));
    }

    [Fact]
    public void Randomized_BarsToTicks_Exactly_2_TicksPerBar()
    {
        var bars = new Bar[]
        {
            new(0, 100, 110, 90, 105, 500, 60),
            new(100, 110, 120, 100, 115, 300, 200)
        };
        var ticks = TickSynthesizer.BarsToTicks(bars, 2, 42);
        Assert.Equal(4, ticks.Length);
    }

    [Fact]
    public void Randomized_BarsToTicks_TickTime_Clamped_At_CloseTime()
    {
        var bars = new Bar[] { new(0, 100, 110, 90, 105, 500, 5) };
        var ticks = TickSynthesizer.BarsToTicks(bars, 5, 42);
        // All intermediate tick times must be < CloseTime
        for (int i = 1; i < ticks.Length - 1; i++)
        {
            Assert.True(ticks[i].Time < bars[0].CloseTime);
        }
    }

    [Fact]
    public void Randomized_BarsToTicks_CloseTime_Equals_OpenTime()
    {
        var bars = new Bar[] { new(100, 100, 100, 100, 100, 0, 100) };
        var ticks = TickSynthesizer.BarsToTicks(bars, 3, 42);
        Assert.Equal(3, ticks.Length);
    }

    [Fact]
    public void ComputeStreamMetrics_Null_Stream_Element_Returns_Defaults()
    {
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { null!, null! });
        Assert.Equal(5000, est);
        Assert.Equal(0, max);
    }

    [Fact]
    public void ComputeAndStoreCompletedBar_Empty_Buffer_Does_Not_Store()
    {
        using var window = new TickWindow(symbolsArray, new[] { TimeFrame.M1 });
        // No ticks pushed — TryGetLastCompletedBar should return false
        Assert.False(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    private static readonly string[] symbols = new[] { "EURUSD" };
    private static readonly string[] symbolsArray = new[] { "EURUSD" };

    [Fact]
    public void TryGetLastCompletedBar_After_Second_Completion_Returns_Latest()
    {
        using var window = new TickWindow(symbols, new[] { TimeFrame.M1 });
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(minute, 2, 2, 1));
        window.PushTick("EURUSD", new Tick(minute + 1, 3, 3, 1));
        window.PushTick("EURUSD", new Tick(minute * 2, 4, 4, 1));

        Assert.True(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out double o, out _, out _, out _, out _));
        Assert.Equal(2, o);
    }

    [Fact]
    public void Bar_Equality_Different_Fields()
    {
        var b1 = new Bar(100, 1.0, 2.0, 0.5, 1.5, 500, 200);
        var b2 = new Bar(100, 1.0, 2.0, 0.5, 1.5, 500, 200);
        var b3 = new Bar(200, 1.0, 2.0, 0.5, 1.5, 500, 200); // different OpenTime
        var b4 = new Bar(100, 1.1, 2.0, 0.5, 1.5, 500, 200); // different Open
        var b5 = new Bar(100, 1.0, 2.1, 0.5, 1.5, 500, 200); // different High
        var b6 = new Bar(100, 1.0, 2.0, 0.6, 1.5, 500, 200); // different Low
        var b7 = new Bar(100, 1.0, 2.0, 0.5, 1.6, 500, 200); // different Close
        var b8 = new Bar(100, 1.0, 2.0, 0.5, 1.5, 501, 200); // different Volume
        var b9 = new Bar(100, 1.0, 2.0, 0.5, 1.5, 500, 201); // different CloseTime

        Assert.True(b1 == b2);
        Assert.False(b1 == b3);
        Assert.False(b1 == b4);
        Assert.False(b1 == b5);
        Assert.False(b1 == b6);
        Assert.False(b1 == b7);
        Assert.False(b1 == b8);
        Assert.False(b1 == b9);
    }

    [Fact]
    public void ComputeStreamMetrics_All_Streams_Empty_Returns_Defaults()
    {
        var (est, max) = TickSynthesizer.ComputeStreamMetrics(new IReadOnlyList<Tick>[] { Array.Empty<Tick>(), Array.Empty<Tick>() });
        Assert.Equal(5000, est);
        Assert.Equal(0, max);
    }
}
