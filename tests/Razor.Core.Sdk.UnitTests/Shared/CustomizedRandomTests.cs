using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class CustomizedRandomTests
{
    [Fact]
    public void Negative_Seed_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CustomizedRandom(-1));
    }

    [Fact]
    public void Zero_Seed_Allowed()
    {
        var rng = new CustomizedRandom(0);
        Assert.NotNull(rng);
    }

    [Fact]
    public void Same_Seed_Produces_Identical_Sequence()
    {
        var rng1 = new CustomizedRandom(123);
        var rng2 = new CustomizedRandom(123);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.NextUInt64(), rng2.NextUInt64());
        }
    }

    [Fact]
    public void Different_Seeds_Different_Sequence()
    {
        var rng1 = new CustomizedRandom(1);
        var rng2 = new CustomizedRandom(2);
        Assert.NotEqual(rng1.NextUInt64(), rng2.NextUInt64());
    }

    [Fact]
    public void NextDouble_Between_0_and_1()
    {
        var rng = new CustomizedRandom(42);
        for (int i = 0; i < 1000; i++)
        {
            Assert.InRange(rng.NextDouble(), 0.0, 1.0 - double.Epsilon);
        }
    }

    [Fact]
    public void Next_Int_NonNegative_LessThan_Max()
    {
        var rng = new CustomizedRandom(0);
        int v = rng.Next(10);
        Assert.InRange(v, 0, 9);
    }

    [Fact]
    public void Next_MinMax_InRange()
    {
        var rng = new CustomizedRandom(7);
        for (int i = 0; i < 100; i++)
        {
            Assert.InRange(rng.Next(10, 20), 10, 19);
        }
    }

    [Fact]
    public void Next_Zero_Max_Returns_Zero()
    {
        var rng = new CustomizedRandom(1);
        Assert.Equal(0, rng.Next(0));
    }

    [Fact]
    public void Next_MinMax_Equal_Returns_MinValue()
    {
        var rng = new CustomizedRandom(42);
        Assert.Equal(5, rng.Next(5, 5));
    }

    [Fact]
    public void Next_MinMax_Deterministic()
    {
        var rng1 = new CustomizedRandom(123);
        var rng2 = new CustomizedRandom(123);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.Next(10, 20), rng2.Next(10, 20));
        }
    }

    [Fact]
    public void ULong_Seed_Works()
    {
        Assert.IsType<CustomizedRandom>(new CustomizedRandom(ulong.MaxValue));
    }

    [Fact]
    public void NextBytes_Fills_Buffer()
    {
        var rng = new CustomizedRandom(42);
        var buf = new byte[100];
        rng.NextBytes(buf);
        Assert.NotEqual(new byte[100], buf);
    }

    [Fact]
    public void NextDouble_MaxValue_In_Range()
    {
        var rng = new CustomizedRandom(42);
        for (int i = 0; i < 100; i++)
        {
            double d = rng.NextDouble(5.0);
            Assert.InRange(d, 0.0, 5.0 - double.Epsilon);
        }
    }

    [Fact]
    public void NextDouble_MinMax_In_Range()
    {
        var rng = new CustomizedRandom(42);
        for (int i = 0; i < 100; i++)
        {
            double d = rng.NextDouble(10.0, 20.0);
            Assert.InRange(d, 10.0, 20.0 - double.Epsilon);
        }
    }

    [Fact]
    public void NextBytes_Null_Buffer_Throws()
    {
        var rng = new CustomizedRandom(42);
        Assert.Throws<ArgumentNullException>(() => rng.NextBytes(null!));
    }
}
