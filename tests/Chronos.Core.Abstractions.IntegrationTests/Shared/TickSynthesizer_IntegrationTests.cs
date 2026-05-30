using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

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
        Assert.Equal(ticks1, ticks2); // determinism check

        Assert.Equal(4000, ticks1.Length); // 4 ticks per bar
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
}
