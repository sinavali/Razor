using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class ChronosRandomTests
{
    [Fact]
    public void Negative_Seed_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChronosRandom(-1));
    }

    [Fact]
    public void Zero_Seed_Allowed()
    {
        var rng = new ChronosRandom(0);
        Assert.NotNull(rng);
    }

    [Fact]
    public void Same_Seed_Produces_Identical_Sequence()
    {
        var rng1 = new ChronosRandom(123);
        var rng2 = new ChronosRandom(123);
        for (int i = 0; i < 100; i++)
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
            Assert.InRange(rng.NextDouble(), 0.0, 1.0 - double.Epsilon);
        }
    }

    [Fact]
    public void Next_Int_NonNegative_LessThan_Max()
    {
        var rng = new ChronosRandom(0);
        int v = rng.Next(10);
        Assert.InRange(v, 0, 9);
    }

    [Fact]
    public void Next_MinMax_InRange()
    {
        var rng = new ChronosRandom(7);
        for (int i = 0; i < 100; i++)
        {
            Assert.InRange(rng.Next(10, 20), 10, 19);
        }
    }

    [Fact]
    public void Next_Zero_Max_Returns_Zero()
    {
        // ChronosRandom.Next(0) returns 0 gracefully.
        var rng = new ChronosRandom(1);
        Assert.Equal(0, rng.Next(0));
    }

    [Fact]
    public void Next_MinMax_Equal_Returns_MinValue()
    {
        // When min >= max, Next(min, max) returns min.
        var rng = new ChronosRandom(42);
        Assert.Equal(5, rng.Next(5, 5));
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

    [Fact]
    public void ULong_Seed_Works()
    {
        Assert.IsType<ChronosRandom>(new ChronosRandom(ulong.MaxValue));
    }

    [Fact]
    public void NextBytes_Fills_Buffer()
    {
        var rng = new ChronosRandom(42);
        var buf = new byte[100];
        rng.NextBytes(buf);
        Assert.NotEqual(new byte[100], buf);
    }
}
