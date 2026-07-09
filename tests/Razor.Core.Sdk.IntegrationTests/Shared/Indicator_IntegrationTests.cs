using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

internal sealed class TestableIndicator : Indicator
{
    public override void Calculate(long index)
    {
    }
}

public class Indicator_IntegrationTests
{
    [Fact]
    public void Initialize_With_Large_Capacity_And_Wrap_Around()
    {
        using var ind = new TestableIndicator();
        ind.Initialize(10000);
        for (long i = 0; i < 20000; i++)
        {
            ind[i] = i;
        }

        Assert.Equal(19999, ind[19999]);
        Assert.Equal(10000, ind[10000]);
    }

    [Fact]
    public void Double_Dispose_And_Reinitialize()
    {
        var ind = new TestableIndicator();
        ind.Initialize(1000);
        ind[0] = 42;
        ind.Dispose();
        ind.Dispose();

        ind.Initialize(500);
        ind[0] = 99;
        Assert.Equal(99, ind[0]);
        ind.Dispose();
    }

    [Fact]
    public void Wrap_Millions_Of_Ticks()
    {
        using var ind = new TestableIndicator();
        ind.Initialize(1000);
        for (long i = 0; i < 10_000_000; i++)
        {
            ind[i] = i;
        }

        Assert.Equal(9_999_999, ind[9_999_999]);
        Assert.Equal(9_999_000, ind[9_999_000]);
    }

    [Fact]
    public void Set_And_Get_Across_Wraps()
    {
        using var ind = new TestableIndicator();
        ind.Initialize(256);
        for (long i = 0; i < 1000; i++)
        {
            ind[i] = i * 2;
        }

        Assert.Equal(1998, ind[999]);
        Assert.Equal(1500, ind[750]);
    }

    [Fact]
    public void Signature_Default_Is_Empty()
    {
        using var ind = new TestableIndicator();
        Assert.Equal(string.Empty, ind.Signature);
    }

    private static readonly string[] symbols = new[] { "X" };
    private static readonly string[] symbolsArray = new[] { "X" };

    [Fact]
    public void SetWindow_Injects_Window()
    {
        using var ind = new TestableIndicator();
        using var window = new TickWindow(symbols, new[] { TimeFrame.M1 });
        ((IWindowAwareIndicator)ind).SetWindow(window);
    }

    [Fact]
    public void Initialize_Negative_Capacity_Throws()
    {
        using var ind = new TestableIndicator();
        Assert.Throws<ArgumentException>(() => ind.Initialize(-1));
    }

    [Fact]
    public void Initialize_Zero_Capacity_Throws()
    {
        using var ind = new TestableIndicator();
        Assert.Throws<ArgumentException>(() => ind.Initialize(0));
    }

    [Fact]
    public void SetWindow_Then_Access_Window()
    {
        using var ind = new TestableIndicator();
        using var window = new TickWindow(symbolsArray, new[] { TimeFrame.M1 });
        ((IWindowAwareIndicator)ind).SetWindow(window);
        // Window is now set; verify it doesn't throw on access
    }
}
