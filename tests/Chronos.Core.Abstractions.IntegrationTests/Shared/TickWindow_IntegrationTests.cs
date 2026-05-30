using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class TickWindow_IntegrationTests
{
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
            // alternate symbols and advance time by 30 seconds
            string sym = i % 2 == 0 ? "EURUSD" : "GBPUSD";
            long time = baseTime + i * TimeSpan.TicksPerSecond * 30;
            window.PushTick(sym, new Tick(time, 1.0 + i * 0.0001, 1.0 + i * 0.0001, 1));
        }

        // At least one window should have completed
        Assert.True(eventCount["EURUSD"] > 0 || eventCount["GBPUSD"] > 0);

        // Verify we can retrieve recent ticks
        var recent = window.GetRecentTicks("EURUSD", 10);
        Assert.InRange(recent.Count, 1, 10);
    }
}
