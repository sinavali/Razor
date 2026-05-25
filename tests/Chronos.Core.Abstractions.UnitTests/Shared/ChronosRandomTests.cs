using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class ChronosRandomTests
{
    [Fact]
    public void Same_Seed_Produces_Identical_Sequence()
    {
        var rng1 = new ChronosRandom(123);
        var rng2 = new ChronosRandom(123);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(rng1.NextUInt64(), rng2.NextUInt64());
        }
    }

    [Fact]
    public void Different_Seeds_Different_Sequence()
    {
        var rng1 = new ChronosRandom(1);
        var rng2 = new ChronosRandom(2);
        Assert.NotEqual(rng1.NextUInt64(), rng2.NextUInt64());
    }

    [Fact]
    public void NextDouble_Between_0_and_1()
    {
        var rng = new ChronosRandom(42);
        for (int i = 0; i < 1000; i++)
        {
            double d = rng.NextDouble();
            Assert.InRange(d, 0.0, 1.0 - double.Epsilon);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public void Next_MaxValue_Returns_NonNegative_LessThan_Max(int max)
    {
        var rng = new ChronosRandom(0);
        int v = rng.Next(max);
        Assert.InRange(v, 0, max - 1);
    }

    [Fact]
    public void Next_MinMax_Returns_InRange()
    {
        var rng = new ChronosRandom(7);
        for (int i = 0; i < 100; i++)
        {
            int v = rng.Next(10, 20);
            Assert.InRange(v, 10, 19);
        }
    }

    [Fact]
    public void Next_Zero_MaxValue_Throws_DivideByZero()
    {
        var rng = new ChronosRandom(1);
        Assert.Throws<DivideByZeroException>(() => rng.Next(0));
    }

    [Fact]
    public void ULong_Seed_Works()
    {
        var rng = new ChronosRandom(ulong.MaxValue);
        Assert.IsType<ChronosRandom>(rng);
    }
}
