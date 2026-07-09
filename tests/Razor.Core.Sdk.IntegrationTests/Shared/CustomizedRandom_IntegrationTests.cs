using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class CustomizedRandom_IntegrationTests
{
    [Fact]
    public void Deterministic_Across_One_Million_Calls()
    {
        var rng1 = new CustomizedRandom(12345);
        var rng2 = new CustomizedRandom(12345);

        for (int i = 0; i < 1_000_000; i++)
        {
            Assert.Equal(rng1.NextUInt64(), rng2.NextUInt64());
        }
    }

    [Fact]
    public void NextDouble_Uniform_Distribution_Check()
    {
        var rng = new CustomizedRandom(42);
        int[] buckets = new int[10];
        int samples = 100_000;

        for (int i = 0; i < samples; i++)
        {
            double d = rng.NextDouble();
            int bucket = (int)(d * 10);
            if (bucket >= 10)
            {
                bucket = 9;
            }

            buckets[bucket]++;
        }

        int expected = samples / 10;
        foreach (int count in buckets)
        {
            Assert.InRange(count, expected * 80 / 100, expected * 120 / 100);
        }
    }

    [Fact]
    public void NextBytes_Fills_Large_Buffer()
    {
        var rng = new CustomizedRandom(42);
        var buf = new byte[10000];
        rng.NextBytes(buf);

        var zeroBuf = new byte[10000];
        Assert.NotEqual(zeroBuf, buf);
    }

    [Fact]
    public void Five_Million_Calls_Deterministic()
    {
        var rng1 = new CustomizedRandom(999);
        var rng2 = new CustomizedRandom(999);
        for (int i = 0; i < 5_000_000; i++)
        {
            Assert.Equal(rng1.NextUInt64(), rng2.NextUInt64());
        }
    }

    [Fact]
    public void NextDouble_Range_Uniform()
    {
        var rng = new CustomizedRandom(42);
        int below = 0;
        int above = 0;
        for (int i = 0; i < 100_000; i++)
        {
            double d = rng.NextDouble(0.5, 1.5);
            if (d < 1.0)
            {
                below++;
            }
            else
            {
                above++;
            }
        }

        Assert.InRange(below, 42500, 57500);
        Assert.InRange(above, 42500, 57500);
    }

    [Fact]
    public void NextBytes_Megabyte_Fill()
    {
        var rng = new CustomizedRandom(42);
        var buf = new byte[1024 * 1024];
        rng.NextBytes(buf);
        int zeroCount = buf.Count(b => b == 0);
        Assert.True(zeroCount < buf.Length);
    }

    [Fact]
    public void Next_With_Min_Greater_Than_Max_Returns_Min()
    {
        var rng = new CustomizedRandom(42);
        Assert.Equal(10, rng.Next(10, 5));
    }

    [Fact]
    public void NextDouble_Overloads_Consistent()
    {
        var rng = new CustomizedRandom(42);
        double d1 = rng.NextDouble();
        Assert.InRange(d1, 0.0, 1.0);

        double d2 = rng.NextDouble(10.0);
        Assert.InRange(d2, 0.0, 10.0);

        double d3 = rng.NextDouble(5.0, 15.0);
        Assert.InRange(d3, 5.0, 15.0);
    }

    [Fact]
    public void NextBytes_Partial_Fill()
    {
        var rng = new CustomizedRandom(42);
        var buf = new byte[256];
        rng.NextBytes(buf);

        var zeroBuf = new byte[256];
        Assert.NotEqual(zeroBuf, buf);
    }

    [Fact]
    public void NextBytes_Null_Buffer_Throws()
    {
        var rng = new CustomizedRandom(42);
        Assert.Throws<ArgumentNullException>(() => rng.NextBytes(null!));
    }

    [Fact]
    public void Next_Zero_Max_Returns_Zero()
    {
        var rng = new CustomizedRandom(42);
        Assert.Equal(0, rng.Next(0));
    }

    [Fact]
    public void Next_Negative_Max_Returns_Zero()
    {
        var rng = new CustomizedRandom(42);
        Assert.Equal(0, rng.Next(-5));
    }

    [Fact]
    public void NextDouble_MaxValue_Is_Positive()
    {
        var rng = new CustomizedRandom(42);
        double d = rng.NextDouble(100.0);
        Assert.InRange(d, 0.0, 100.0);
    }

    [Fact]
    public void NextDouble_MinMax_Exact_Range()
    {
        var rng = new CustomizedRandom(42);
        double d = rng.NextDouble(10.0, 20.0);
        Assert.InRange(d, 10.0, 20.0);
    }
}
