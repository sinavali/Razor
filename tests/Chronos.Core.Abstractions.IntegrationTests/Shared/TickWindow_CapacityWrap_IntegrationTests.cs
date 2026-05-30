using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class TickWindow_CapacityWrap_IntegrationTests
{
    private static readonly string[] Symbol = { "X" };
    private static readonly TimeFrame[] TimeframeM1 = { TimeFrame.M1 };

    [Fact]
    public void Capacity_Wraps_And_Drops_Oldest_Tick()
    {
        const int capacity = 5;
        using var window = new TickWindow(Symbol, TimeframeM1, maxTicksPerSymbol: capacity);

        // Fill the buffer completely
        for (long t = 0; t < capacity; t++)
        {
            window.PushTick("X", new Tick(t, t, t, t));
        }

        // Push one more to wrap
        window.PushTick("X", new Tick(capacity, capacity, capacity, capacity));

        var recent = window.GetRecentTicks("X", capacity);
        Assert.Equal(capacity, recent.Count);

        // The oldest tick (t=0) should be gone; now we have t=1..5
        Assert.Equal(1, recent[0].Time);
        Assert.Equal(capacity, recent[^1].Time);
    }
}
