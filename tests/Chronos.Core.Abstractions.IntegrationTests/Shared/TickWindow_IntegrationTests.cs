using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class TickWindow_IntegrationTests
{
    private static readonly string[] SymbolsX = { "X" };
    private static readonly string[] SymbolsEurUsd = { "EURUSD" };
    private static readonly TimeFrame[] TimeframeM1 = { TimeFrame.M1 };
    private static readonly string[] symbols = new[] { "X" };
    private static readonly string[] symbolsArray = new[] { "X" };
    private static readonly string[] symbolsArray0 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray1 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray2 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray3 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray4 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray5 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray6 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray7 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray8 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray9 = new[] { "X" };
    private static readonly string[] symbolsArray10 = new[] { "X" };
    private static readonly string[] symbolsArray11 = new[] { "EURUSD" };
    private static readonly string[] symbolsArray12 = new[] { "X" };

    [Fact]
    public void Process_One_Thousand_Ticks_Across_Two_Symbols_With_Multiple_Timeframes()
    {
        var symbols = new[] { "EURUSD", "GBPUSD" };
        var timeframes = new[] { TimeFrame.M1, TimeFrame.M5 };
        using var window = new TickWindow(symbols, timeframes, maxTicksPerSymbol: 2000);

        var eventCount = new Dictionary<string, int>
        {
            { "EURUSD", 0 },
            { "GBPUSD", 0 }
        };

        window.WindowCompleted += (sym, _) => eventCount[sym]++;

        long baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        for (int i = 0; i < 1000; i++)
        {
            string sym = i % 2 == 0 ? "EURUSD" : "GBPUSD";
            long time = baseTime + i * TimeSpan.TicksPerSecond * 30;
            window.PushTick(sym, new Tick(time, 1.0 + i * 0.0001, 1.0 + i * 0.0001, 1));
        }

        Assert.True(eventCount["EURUSD"] > 0 || eventCount["GBPUSD"] > 0);

        var recent = window.GetRecentTicks("EURUSD", 10);
        Assert.InRange(recent.Count, 1, 10);
    }

    [Fact]
    public void Capacity_Wraps_And_Drops_Oldest_Tick()
    {
        const int capacity = 5;
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: capacity);

        for (long t = 0; t < capacity; t++)
        {
            window.PushTick("X", new Tick(t, t, t, t));
        }

        window.PushTick("X", new Tick(capacity, capacity, capacity, capacity));

        var recent = window.GetRecentTicks("X", capacity);
        Assert.Equal(capacity, recent.Count);
        Assert.Equal(1, recent[0].Time);
        Assert.Equal(capacity, recent[^1].Time);
    }

    [Fact]
    public void CopyRecentTicks_With_Large_Buffer()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 1000);
        for (long t = 0; t < 500; t++)
        {
            window.PushTick("EURUSD", new Tick(t, t, t, t));
        }

        Span<Tick> destination = stackalloc Tick[10];
        int count = window.CopyRecentTicks("EURUSD", destination, 10);
        Assert.Equal(10, count);
        Assert.Equal(490, destination[0].Time);
        Assert.Equal(499, destination[^1].Time);
    }

    [Fact]
    public void GetCurrentStats_Across_Many_Windows()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 10000);
        long minute = TimeSpan.TicksPerMinute;

        for (int i = 0; i < 100; i++)
        {
            window.PushTick("EURUSD", new Tick(minute * i, 1.0 + i, 1.0 + i, 1));
        }

        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out double h, out double l, out double c, out double v, out bool ic);

        Assert.Equal(100, o);
        Assert.Equal(100, c);
        Assert.Equal(1, v);
        Assert.True(ic);
    }

    [Fact]
    public void Process_Two_Thousand_Ticks_In_Large_Buffer()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 200_000);
        for (int i = 0; i < 2000; i++)
        {
            window.PushTick("X", new Tick(i * 5, 100 + i, 100 + i, 1));
        }

        window.GetCurrentStats("X", TimeFrame.M1, PriceType.Bid, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(100, o);
        Assert.InRange(h, 100, 100 + 2000);
        Assert.True(v > 0);
        Assert.True(ic);

        var recent = window.GetRecentTicks("X", 10);
        Assert.Equal(10, recent.Count);
    }

    [Fact]
    public void Three_Symbols_Five_Timeframes_Window_Completion()
    {
        var symbols = new[] { "A", "B", "C" };
        var tfs = new[] { TimeFrame.M1, TimeFrame.M5, TimeFrame.M15, TimeFrame.H1, TimeFrame.D1 };
        using var window = new TickWindow(symbols, tfs, maxTicksPerSymbol: 10_000);
        var completions = new Dictionary<string, int> { { "A", 0 }, { "B", 0 }, { "C", 0 } };
        window.WindowCompleted += (sym, _) => completions[sym]++;

        long minute = TimeSpan.TicksPerMinute;
        for (int i = 0; i < 5000; i++)
        {
            string sym = symbols[i % 3];
            window.PushTick(sym, new Tick(minute * i, 1, 1, 1));
        }

        Assert.True(completions["A"] > 0);
        Assert.True(completions["B"] > 0);
        Assert.True(completions["C"] > 0);
    }

    [Fact]
    public void PushTick_With_Tick_Timeframe_Does_Not_Fire_WindowComplete()
    {
        bool fired = false;
        using var window = new TickWindow(symbolsArray2, new[] { TimeFrame.Tick });
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(fired);
    }

    [Fact]
    public void TryGetLastCompletedBar_Returns_False_When_IsComplete_False()
    {
        using var window = new TickWindow(symbolsArray3, new[] { TimeFrame.M1 });
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        Assert.False(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void CopyRecentTicks_Empty_Buffer_Returns_Zero()
    {
        using var window = new TickWindow(symbolsArray1, new[] { TimeFrame.M1 });
        Span<Tick> destination = stackalloc Tick[5];
        int count = window.CopyRecentTicks("EURUSD", destination, 5);
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetRecentTicks_Request_More_Than_Available()
    {
        using var window = new TickWindow(symbolsArray0, new[] { TimeFrame.M1 }, maxTicksPerSymbol: 100);
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(1, 2, 2, 1));
        window.PushTick("EURUSD", new Tick(2, 3, 3, 1));

        var recent = window.GetRecentTicks("EURUSD", 10);
        Assert.Equal(3, recent.Count);
    }

    [Fact]
    public void GetCurrentStats_Window_Complete_When_First_Tick_At_Buffer_Start()
    {
        using var window = new TickWindow(symbolsArray, new[] { TimeFrame.M1 }, maxTicksPerSymbol: 3);
        window.PushTick("X", new Tick(0, 5, 5, 1));
        window.PushTick("X", new Tick(TimeSpan.TicksPerMinute, 10, 10, 1));
        window.GetCurrentStats("X", TimeFrame.M1, PriceType.Bid, out _, out _, out _, out _, out _, out bool ic);
        Assert.True(ic);
    }

    [Fact]
    public void Window_Completed_With_M1_Timeframe()
    {
        int count = 0;
        using var window = new TickWindow(symbols, new[] { TimeFrame.M1 });
        window.WindowCompleted += (_, _) => count++;

        long minute = TimeSpan.TicksPerMinute;
        for (int i = 0; i < 10; i++)
        {
            window.PushTick("X", new Tick(minute * i, i, i, 1));
        }

        Assert.True(count > 0);
    }

    [Fact]
    public void GetCurrentStats_Unknown_Symbol_Returns_Zeros()
    {
        using var window = new TickWindow(symbolsArray5, new[] { TimeFrame.M1 });
        window.GetCurrentStats("UNKNOWN", TimeFrame.M1, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void CopyRecentTicks_Count_Zero_Returns_Zero()
    {
        using var window = new TickWindow(symbolsArray4, new[] { TimeFrame.M1 });
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        Span<Tick> destination = stackalloc Tick[5];
        int count = window.CopyRecentTicks("EURUSD", destination, 0);
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetCurrentStats_Tick_Timeframe_Returns_Early()
    {
        using var window = new TickWindow(symbolsArray9, new[] { TimeFrame.M1 });
        window.PushTick("X", new Tick(0, 1, 1, 1));
        window.GetCurrentStats("X", TimeFrame.Tick, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void TryGetLastCompletedBar_Not_Found_Different_Timeframe()
    {
        using var window = new TickWindow(symbolsArray8, new[] { TimeFrame.M1 });
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(window.TryGetLastCompletedBar("EURUSD", TimeFrame.H1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void TryGetLastCompletedBar_Not_Found_Different_Symbol()
    {
        using var window = new TickWindow(symbolsArray7, new[] { TimeFrame.M1 });
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(window.TryGetLastCompletedBar("GBPUSD", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void WindowCompleted_Not_Fired_For_Tick_Timeframe()
    {
        bool fired = false;
        using var window = new TickWindow(symbolsArray6, new[] { TimeFrame.Tick });
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(fired);
    }

    [Fact]
    public void ComputeAndStoreCompletedBar_Tick_Timeframe_Does_Nothing()
    {
        // Pushing ticks when only Tick timeframe is tracked – no window completion
        bool fired = false;
        using var window = new TickWindow(symbolsArray11, new[] { TimeFrame.Tick });
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(fired);
    }

    [Fact]
    public void GetCurrentStats_Tick_Timeframe_Returns_Zeros()
    {
        using var window = new TickWindow(symbolsArray10, new[] { TimeFrame.M1 });
        window.PushTick("X", new Tick(0, 1, 1, 1));
        window.GetCurrentStats("X", TimeFrame.Tick, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void GetCurrentStats_Tick_Timeframe_Returns_Early_2()
    {
        // Already covered, but ensure the periodTicks <= 0 branch is hit with Tick
        using var window = new TickWindow(symbolsArray12, new[] { TimeFrame.M1 });
        window.PushTick("X", new Tick(0, 1, 1, 1));
        window.GetCurrentStats("X", TimeFrame.Tick, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }
}
