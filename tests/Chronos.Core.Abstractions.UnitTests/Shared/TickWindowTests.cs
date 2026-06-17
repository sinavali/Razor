using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class TickWindowTests
{
    private static readonly string[] SymbolsEurUsd = { "EURUSD" };
    private static readonly string[] SymbolsX = { "X" };
    private static readonly TimeFrame[] TimeframeM1 = { TimeFrame.M1 };
    private static readonly TimeFrame[] TimeframeH1 = { TimeFrame.H1 };
    private static readonly TimeFrame[] TimeframeTick = { TimeFrame.Tick };
    private static readonly TimeFrame[] AllTimeframes = { TimeFrame.M1, TimeFrame.H1 };

    // ── PushTick & WindowCompleted ───────────────────────────────

    [Fact]
    public void PushTick_Fires_WindowCompleted_When_Period_Ends()
    {
        bool fired = false;
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 10);
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1, 2, 0));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 1, 2, 0));
        Assert.True(fired);
    }

    [Fact]
    public void WindowCompleted_Fires_For_Multiple_Timeframes_On_Same_Tick()
    {
        using var window = new TickWindow(SymbolsEurUsd, AllTimeframes, maxTicksPerSymbol: 100);
        var firedTimeframes = new List<TimeFrame>();
        window.WindowCompleted += (_, tf) => firedTimeframes.Add(tf);
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute * 60, 2, 2, 1));
        Assert.Contains(TimeFrame.M1, firedTimeframes);
        Assert.Contains(TimeFrame.H1, firedTimeframes);
    }

    [Fact]
    public void PushTick_No_Fire_When_No_Timeframes()
    {
        bool fired = false;
        using var window = new TickWindow(SymbolsX, TimeframeTick);
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("X", new Tick(0, 0, 0, 0));
        window.PushTick("X", new Tick(TimeSpan.TicksPerMinute, 0, 0, 0));
        Assert.False(fired);
    }

    [Fact]
    public void PushTick_Ignores_Unknown_Symbol()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("Unknown", new Tick(0, 0, 0, 0));

        // Unknown symbol has no buffer — verify no data is stored and no events fire.
        Assert.Empty(window.GetRecentTicks("Unknown", 5));
        Assert.False(window.TryGetLastCompletedBar("Unknown", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void PushTick_After_Dispose_Does_Not_Throw()
    {
        var window = new TickWindow(SymbolsX, TimeframeM1);
        window.Dispose();
        window.PushTick("X", new Tick(0, 1, 1, 1));

        // After disposal, all buffers are cleared, so queries return empty/zero.
        Assert.Empty(window.GetRecentTicks("X", 5));
        window.GetCurrentStats("X", TimeFrame.M1, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void Tick_Timeframe_Not_In_Timeframes_Does_Not_Complete()
    {
        bool fired = false;
        using var window = new TickWindow(SymbolsEurUsd, TimeframeH1);
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 0, 0, 0));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute * 30, 0, 0, 0));
        Assert.False(fired);
    }

    // ── GetCurrentStats ──────────────────────────────────────────

    [Fact]
    public void GetCurrentStats_Returns_OHLC_Bid()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(0, 1.5, 1.6, 1));
        window.PushTick("EURUSD", new Tick(0, 1.7, 1.8, 2));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(1.5, o);
        Assert.Equal(1.7, h);
        Assert.Equal(1.5, l);
        Assert.Equal(1.7, c);
        Assert.Equal(3.0, v);
        Assert.True(ic);
    }

    [Fact]
    public void GetCurrentStats_Ask_PriceType()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(0, 1.0, 1.5, 1));
        window.PushTick("EURUSD", new Tick(0, 1.2, 1.7, 2));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Ask, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(1.5, o);
        Assert.Equal(1.7, h);
        Assert.Equal(1.5, l);
        Assert.Equal(1.7, c);
    }

    [Fact]
    public void GetCurrentStats_Mid_PriceType()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(0, 1.0, 2.0, 1));
        window.PushTick("EURUSD", new Tick(0, 2.0, 4.0, 2));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Mid, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(1.5, o);
        Assert.Equal(3.0, h);
        Assert.Equal(1.5, l);
        Assert.Equal(3.0, c);
    }

    [Fact]
    public void GetCurrentStats_IsComplete_When_Previous_Tick_Was_Before_Window()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        long m = TimeSpan.TicksPerMinute;
        window.PushTick("X", new Tick(0, 0, 0, 0));
        window.PushTick("X", new Tick(m * 2, 1, 1, 0));
        window.PushTick("X", new Tick(m * 2 + 1, 2, 2, 0));
        window.GetCurrentStats("X", TimeFrame.M1, PriceType.Bid, out _, out _, out _, out _, out _, out bool complete);
        Assert.True(complete);
    }

    [Fact]
    public void GetCurrentStats_Window_Complete_When_First_Tick_Starts_Window()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("EURUSD", new Tick(minute * 2, 1.0, 1.0, 1));
        window.PushTick("EURUSD", new Tick(minute * 2 + 1, 2.0, 2.0, 1));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out _, out _, out _, out _, out _, out bool complete);
        Assert.True(complete);
    }

    [Fact]
    public void GetCurrentStats_Missing_Symbol_Returns_Zeros()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.GetCurrentStats("B", TimeFrame.M1, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void GetCurrentStats_Empty_Buffer_Returns_Zeros()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void GetCurrentStats_H1_Timeframe_Works()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeH1);
        long hour = TimeSpan.TicksPerHour;
        window.PushTick("EURUSD", new Tick(0, 10, 10, 1));
        window.PushTick("EURUSD", new Tick(hour, 20, 20, 1));

        // Previous window completed, verify its OHLC
        Assert.True(window.TryGetLastCompletedBar("EURUSD", TimeFrame.H1, out double o, out double h, out double l, out double c, out double v));
        Assert.Equal(10, o);
        Assert.Equal(10, h);
        Assert.Equal(10, l);
        Assert.Equal(10, c);
        Assert.Equal(1, v);

        // Current window is incomplete
        window.GetCurrentStats("EURUSD", TimeFrame.H1, PriceType.Bid, out double co, out _, out _, out _, out _, out bool cic);
        Assert.Equal(20, co);
        Assert.True(cic);
    }

    [Fact]
    public void GetCurrentStats_With_Multiple_Ticks_Before_Current_Window()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 10);
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("EURUSD", new Tick(0, 0, 0, 0));
        window.PushTick("EURUSD", new Tick(minute * 1, 1, 1, 0));
        window.PushTick("EURUSD", new Tick(minute * 1 + 1, 2, 2, 0));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(1, o);
        Assert.Equal(2, h);
        Assert.Equal(1, l);
        Assert.Equal(2, c);
        Assert.True(ic);
    }

    // ── TryGetLastCompletedBar ──────────────────────────────────

    [Fact]
    public void TryGetLastCompletedBar_Returns_Stored_OHLC()
    {
        bool fired = false;
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1.0, 1.0, 10));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 1.5, 1.5, 20));
        Assert.True(fired);
        Assert.True(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out double o, out double h, out double l, out double c, out double v));
        Assert.Equal(1.0, o);
        Assert.Equal(10, v);
    }

    [Fact]
    public void TryGetLastCompletedBar_Not_Completed_Returns_False()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 1, 1, 1));
        Assert.False(window.TryGetLastCompletedBar("X", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void TryGetLastCompletedBar_Multiple_Completions_Returns_Latest()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(minute * 1, 2, 2, 1));
        window.PushTick("EURUSD", new Tick(minute * 1 + 1, 3, 3, 1));
        window.PushTick("EURUSD", new Tick(minute * 2, 4, 4, 1));
        Assert.True(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out double o, out double h, out double l, out double c, out double v));
        Assert.Equal(2, o);
        Assert.Equal(3, h);
        Assert.Equal(2, l);
        Assert.Equal(3, c);
    }

    [Fact]
    public void TryGetLastCompletedBar_Different_Symbol_Not_Found()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(window.TryGetLastCompletedBar("GBPUSD", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void TryGetLastCompletedBar_Different_Timeframe_Not_Found()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.False(window.TryGetLastCompletedBar("EURUSD", TimeFrame.H1, out _, out _, out _, out _, out _));
    }

    // ── GetRecentTicks ───────────────────────────────────────────

    [Fact]
    public void GetRecentTicks_Returns_Most_Recent()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 5);
        for (long t = 0; t < 5; t++)
        {
            window.PushTick("X", new Tick(t, 0, 0, 0));
        }

        var recent = window.GetRecentTicks("X", 3);
        Assert.Equal(3, recent.Count);
        Assert.Equal(2, recent[0].Time);
        Assert.Equal(4, recent[^1].Time);
    }

    [Fact]
    public void GetRecentTicks_Count_Greater_Than_Buffer_Returns_All()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 2);
        window.PushTick("X", new Tick(1, 0, 0, 0));
        window.PushTick("X", new Tick(2, 0, 0, 0));
        Assert.Equal(2, window.GetRecentTicks("X", 10).Count);
    }

    [Fact]
    public void GetRecentTicks_Unknown_Symbol_Returns_Empty()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        Assert.Empty(window.GetRecentTicks("B", 5));
    }

    [Fact]
    public void GetRecentTicks_Negative_Count_Throws()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 1, 1, 1));
        Assert.Throws<OverflowException>(() => window.GetRecentTicks("X", -1));
    }

    [Fact]
    public void GetRecentTicks_Count_Zero_Returns_Empty_Array()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 1, 1, 1));
        Assert.Empty(window.GetRecentTicks("X", 0));
    }

    // ── CopyRecentTicks ─────────────────────────────────────────

    [Fact]
    public void CopyRecentTicks_Unknown_Symbol_Returns_Zero()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        Span<Tick> destination = stackalloc Tick[10];
        int count = window.CopyRecentTicks("UNKNOWN", destination, 5);
        Assert.Equal(0, count);
    }

    [Fact]
    public void CopyRecentTicks_Returns_Correct_Count()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 5);
        for (long t = 0; t < 5; t++)
        {
            window.PushTick("EURUSD", new Tick(t, 1, 1, 1));
        }

        Span<Tick> destination = stackalloc Tick[3];
        int count = window.CopyRecentTicks("EURUSD", destination, 3);
        Assert.Equal(3, count);
        Assert.Equal(2, destination[0].Time);
        Assert.Equal(4, destination[2].Time);
    }

    [Fact]
    public void CopyRecentTicks_MaxCount_Greater_Than_Buffer_Returns_All()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 2);
        window.PushTick("EURUSD", new Tick(1, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(2, 1, 1, 1));
        Span<Tick> destination = stackalloc Tick[5];
        int count = window.CopyRecentTicks("EURUSD", destination, 5);
        Assert.Equal(2, count);
    }

    [Fact]
    public void CopyRecentTicks_Zero_MaxCount_Returns_Zero()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(1, 1, 1, 1));
        Span<Tick> destination = stackalloc Tick[5];
        int count = window.CopyRecentTicks("EURUSD", destination, 0);
        Assert.Equal(0, count);
    }

    // ── Ring Buffer & Wrapping ───────────────────────────────────

    [Fact]
    public void Ring_Buffer_Wraps_Correctly()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 2);
        window.PushTick("X", new Tick(1, 1, 1, 0));
        window.PushTick("X", new Tick(2, 2, 2, 0));
        window.PushTick("X", new Tick(3, 3, 3, 0));
        var recent = window.GetRecentTicks("X", 2);
        Assert.Equal(2, recent[0].Time);
        Assert.Equal(3, recent[1].Time);
    }

    [Fact]
    public void MaxTicksPerSymbol_One_Wraps_Every_Push()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 1);
        window.PushTick("X", new Tick(100, 1, 2, 3));
        Assert.Equal(100, window.GetRecentTicks("X", 1)[0].Time);
        window.PushTick("X", new Tick(200, 4, 5, 6));
        Assert.Equal(200, window.GetRecentTicks("X", 1)[0].Time);
    }

    // ── Dispose ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_Clears_Buffers()
    {
        var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 0, 0, 0));
        window.Dispose();
        Assert.Empty(window.GetRecentTicks("X", 1));
    }

    // ── Additional GetCurrentStats ──────────────────────────────

    [Fact]
    public void GetCurrentStats_No_Buffer_For_Symbol()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void GetCurrentStats_Tick_Before_Window_Start_Skipped()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 5);
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("EURUSD", new Tick(minute, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(minute + 1, 2, 2, 1));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(1, o);
        Assert.Equal(2, h);
        Assert.Equal(1, l);
        Assert.Equal(2, c);
        Assert.True(ic);
    }

    [Fact]
    public void GetCurrentStats_After_Window_Complete_New_Window_Starts()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("EURUSD", new Tick(0, 5, 5, 1));
        window.PushTick("EURUSD", new Tick(minute, 10, 10, 1));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(10, o);
        Assert.True(ic);
    }

    // ── Additional TryGetLastCompletedBar ──────────────────────

    [Fact]
    public void TryGetLastCompletedBar_No_Complete_Bar_When_Buffer_Empty()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        Assert.False(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void Completed_Bar_Stored_On_Window_Complete()
    {
        bool fired = false;
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.WindowCompleted += (_, _) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1, 1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 2, 2, 1));
        Assert.True(fired);
        Assert.True(window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out double o, out double h, out double l, out double c, out double v));
        Assert.Equal(1, o);
        Assert.Equal(1, h);
        Assert.Equal(1, l);
        Assert.Equal(1, c);
    }
}
