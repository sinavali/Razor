using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class Tick_IntegrationTests
{
    [Fact]
    public void Tick_Equality_Large_Comparison()
    {
        var ticks = new Tick[10000];
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i] = new Tick(i * 1000, i * 0.1, i * 0.1 + 0.05, i % 10, i % 2 == 0);
        }

        for (int i = 0; i < ticks.Length; i++)
        {
            Assert.Equal(ticks[i], ticks[i]);
            if (i > 0)
            {
                Assert.NotEqual(ticks[i - 1], ticks[i]);
            }
        }
    }

    [Fact]
    public void Tick_All_Properties_Consistent()
    {
        var t = new Tick(100, 1.5, 1.6, 10, true);
        Assert.Equal(100, t.Time);
        Assert.Equal(1.5, t.Bid);
        Assert.Equal(1.6, t.Ask);
        Assert.Equal(10, t.Volume);
        Assert.True(t.IsSynthetic);

        var t2 = new Tick(100, 1.5, 1.6, 10, false);
        Assert.NotEqual(t, t2);
    }

    [Fact]
    public void Tick_Boxed_Equality()
    {
        var a = new Tick(42, 1.0, 2.0, 3.0, true);
        object b = new Tick(42, 1.0, 2.0, 3.0, true);
        object c = new Tick(43, 1.0, 2.0, 3.0, true);

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(null));
        Assert.False(a.Equals("string"));
    }

    [Fact]
    public void Tick_Inequality_Operator_Works()
    {
        var a = new Tick(1, 2, 3, 4);
        var b = new Tick(2, 2, 3, 4);
        var c = new Tick(1, 2, 3, 4);

        Assert.True(a != b);
        Assert.False(a != c);
    }

    [Fact]
    public void Tick_Equality_Operator_And_HashCode()
    {
        var a = new Tick(1, 2, 3, 4, true);
        var b = new Tick(1, 2, 3, 4, true);
        var c = new Tick(1, 2, 3, 5, true);
        var d = new Tick(1, 2, 4, 4, true);
        var e = new Tick(1, 3, 3, 4, true);
        var f = new Tick(2, 2, 3, 4, true);
        var g = new Tick(1, 2, 3, 4, false);

        Assert.True(a == b);
        Assert.False(a == c);
        Assert.False(a == d);
        Assert.False(a == e);
        Assert.False(a == f);
        Assert.False(a == g);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
