using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class ChronosRandomAdditionalTests
{
    [Fact]
    public void Next_MinMax_Zero_Range_Throws_DivideByZero()
    {
        var rng = new ChronosRandom(42);
        Assert.Throws<DivideByZeroException>(() => rng.Next(5, 5));
    }

    [Fact]
    public void Next_MinMax_Deterministic()
    {
        var rng1 = new ChronosRandom(123);
        var rng2 = new ChronosRandom(123);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.Next(10, 20), rng2.Next(10, 20));
        }
    }
}
