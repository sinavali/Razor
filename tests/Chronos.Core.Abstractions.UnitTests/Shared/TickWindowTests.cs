using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class TickWindowTests
{
    private static readonly string[] SymbolsEurUsd = { "EURUSD" };
    private static readonly string[] SymbolsX = { "X" };
    private static readonly string[] SymbolsA = { "A" };
    private static readonly TimeFrame[] TimeframeM1 = { TimeFrame.M1 };
    private static readonly TimeFrame[] TimeframeTick = { TimeFrame.Tick };

    [Fact]
    public void PushTick_Fires_WindowCompleted_When_Period_Ends()
    {
        bool fired = false;
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1, maxTicksPerSymbol: 10);
        window.WindowCompleted += (sym, tf) => fired = true;
        window.PushTick("EURUSD", new Tick(0, 1, 2, 0));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 1, 2, 0));
        Assert.True(fired);
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
        using var window = new TickWindow(SymbolsA, TimeframeM1);
        window.PushTick("Unknown", new Tick(0, 0, 0, 0));
        // No exception
    }

    [Fact]
    public void GetCurrentStats_Returns_OHLC_Bid()
    {
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.PushTick("EURUSD", new Tick(0, 1.5, 1.6, 1));
        window.PushTick("EURUSD", new Tick(0, 1.7, 1.8, 2));
        window.GetCurrentStats("EURUSD", TimeFrame.M1, PriceType.Bid,
            out double open, out double high, out double low, out double close, out double volume, out bool isComplete);
        Assert.Equal(1.5, open);
        Assert.Equal(1.7, high);
        Assert.Equal(1.5, low);
        Assert.Equal(1.7, close);
        Assert.Equal(3.0, volume);
        // Both ticks are in the same window with no earlier tick -> the window is complete.
        Assert.True(isComplete);
    }

    [Fact]
    public void GetCurrentStats_IsComplete_When_Previous_Tick_Was_Before_Window()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        long minute = TimeSpan.TicksPerMinute;
        window.PushTick("X", new Tick(minute * 0, 0, 0, 0));
        window.PushTick("X", new Tick(minute * 2, 1, 1, 0));
        window.PushTick("X", new Tick(minute * 2 + 1, 2, 2, 0));
        window.GetCurrentStats("X", TimeFrame.M1, PriceType.Bid, out _, out _, out _, out _, out _, out bool complete);
        Assert.True(complete);
    }

    [Fact]
    public void GetCurrentStats_Missing_Symbol_Returns_Zeros()
    {
        using var window = new TickWindow(SymbolsA, TimeframeM1);
        window.GetCurrentStats("B", TimeFrame.M1, PriceType.Bid, out double o, out double h, out double l, out double c, out double v, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void GetCurrentStats_Tick_Timeframe_Ignored()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 1, 2, 3));
        window.GetCurrentStats("X", TimeFrame.Tick, PriceType.Bid, out double o, out _, out _, out _, out _, out bool ic);
        Assert.Equal(0, o);
        Assert.False(ic);
    }

    [Fact]
    public void TryGetLastCompletedBar_Returns_Stored_OHLC()
    {
        bool eventFired = false;
        using var window = new TickWindow(SymbolsEurUsd, TimeframeM1);
        window.WindowCompleted += (_, _) => eventFired = true;
        window.PushTick("EURUSD", new Tick(0, 1.0, 1.0, 10));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 1.5, 1.5, 20));
        Assert.True(eventFired);
        bool ok = window.TryGetLastCompletedBar("EURUSD", TimeFrame.M1, out double o, out double h, out double l, out double c, out double v);
        Assert.True(ok);
        Assert.Equal(1.0, o);
        Assert.Equal(1.0, h);
        Assert.Equal(1.0, l);
        Assert.Equal(1.0, c);
        Assert.Equal(10, v);
    }

    [Fact]
    public void TryGetLastCompletedBar_Not_Completed_Returns_False()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 1, 1, 1));
        bool ok = window.TryGetLastCompletedBar("X", TimeFrame.M1, out _, out _, out _, out _, out _);
        Assert.False(ok);
    }

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
        // The buffer returns ticks from oldest to newest.
        // After adding ticks 0,1,2,3,4, the most recent 3 are 2,3,4.
        Assert.Equal(2, recent[0].Time);
        Assert.Equal(4, recent[2].Time);
    }

    [Fact]
    public void GetRecentTicks_Count_Greater_Than_Buffer_Returns_All()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 2);
        window.PushTick("X", new Tick(1, 0, 0, 0));
        window.PushTick("X", new Tick(2, 0, 0, 0));
        var recent = window.GetRecentTicks("X", 10);
        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public void GetRecentTicks_Unknown_Symbol_Returns_Empty()
    {
        using var window = new TickWindow(SymbolsA, TimeframeM1);
        var ticks = window.GetRecentTicks("B", 5);
        Assert.Empty(ticks);
    }

    [Fact]
    public void Ring_Buffer_Wraps_Correctly()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 2);
        window.PushTick("X", new Tick(1, 1, 1, 0));
        window.PushTick("X", new Tick(2, 2, 2, 0));
        window.PushTick("X", new Tick(3, 3, 3, 0));
        var recent = window.GetRecentTicks("X", 2);
        Assert.Equal(2, recent.Count);
        // After wrapping, the buffer contains tick(2) and tick(3). Oldest first.
        Assert.Equal(2, recent[0].Time);
        Assert.Equal(3, recent[1].Time);
    }

    [Fact]
    public void Dispose_Clears_Buffers()
    {
        var window = new TickWindow(SymbolsX, TimeframeM1);
        window.PushTick("X", new Tick(0, 0, 0, 0));
        window.Dispose();
        var recent = window.GetRecentTicks("X", 1);
        Assert.Empty(recent);
    }
}
