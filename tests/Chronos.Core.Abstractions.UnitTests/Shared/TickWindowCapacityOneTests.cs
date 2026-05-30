using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class TickWindowCapacityOneTests
{
    private static readonly string[] SymbolsX = { "X" };
    private static readonly TimeFrame[] TimeframeM1 = { TimeFrame.M1 };

    [Fact]
    public void MaxTicksPerSymbol_One_Wraps_Every_Push()
    {
        using var window = new TickWindow(SymbolsX, TimeframeM1, maxTicksPerSymbol: 1);

        window.PushTick("X", new Tick(100, 1, 2, 3));
        var recent = window.GetRecentTicks("X", 5);
        Assert.Single(recent);
        Assert.Equal(100, recent[0].Time);

        window.PushTick("X", new Tick(200, 4, 5, 6));
        recent = window.GetRecentTicks("X", 5);
        Assert.Single(recent);
        Assert.Equal(200, recent[0].Time);

        window.PushTick("X", new Tick(300, 7, 8, 9));
        recent = window.GetRecentTicks("X", 5);
        Assert.Single(recent);
        Assert.Equal(300, recent[0].Time);
    }
}
