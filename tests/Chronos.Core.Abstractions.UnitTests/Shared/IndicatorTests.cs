using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

internal sealed class TestableIndicator : Indicator
{
    public override void Calculate(long index)
    {
    }
}

public class IndicatorTests
{
    private static readonly string[] SymbolsX = ["X"];
    private static readonly TimeFrame[] TimeframeM1 = [TimeFrame.M1];

    [Fact]
    public void Indexer_Get_Returns_Zero_When_Empty()
    {
        using var ind = new TestableIndicator();
        Assert.Equal(0, ind[0]);
    }

    [Fact]
    public void Indexer_Set_And_Get_Wrap_Around()
    {
        using var ind = new TestableIndicator();
        ind.Initialize(4);
        ind[0] = 10;
        ind[4] = 20;
        Assert.Equal(20, ind[0]);
        Assert.Equal(20, ind[4]);
    }

    [Fact]
    public void Initialize_Replaces_Buffer()
    {
        using var ind = new TestableIndicator();
        ind.Initialize(3);
        ind[0] = 5;
        ind.Initialize(5);
        Assert.Equal(0, ind[0]);
    }

    [Fact]
    public void Initialize_Zero_Capacity_Throws()
    {
        using var ind = new TestableIndicator();
        Assert.Throws<ArgumentException>(() => ind.Initialize(0));
    }

    [Fact]
    public void Initialize_Negative_Capacity_Throws()
    {
        using var ind = new TestableIndicator();
        Assert.Throws<ArgumentException>(() => ind.Initialize(-1));
    }

    [Fact]
    public void Double_Dispose_Does_Not_Throw()
    {
        var ind = new TestableIndicator();
        ind.Initialize(2);
        ind.Dispose();
        ind.Dispose();
    }

    [Fact]
    public void Dispose_Releases_Buffer()
    {
        var ind = new TestableIndicator();
        ind.Initialize(2);
        ind[0] = 1;
        ind.Dispose();
        Assert.Equal(0, ind[0]);
    }

    [Fact]
    public void Signature_Default_Empty()
    {
        using var ind = new TestableIndicator();
        Assert.Equal(string.Empty, ind.Signature);
    }

    [Fact]
    public void Set_Value_When_Count_Zero_Throws_DivideByZero()
    {
        using var ind = new TestableIndicator();
        Assert.Throws<DivideByZeroException>(() => ind[0] = 5);
    }

    [Fact]
    public void Reinitialize_After_Dispose_Works()
    {
        var ind = new TestableIndicator();
        ind.Initialize(2);
        ind[0] = 10;
        ind.Dispose();
        ind.Initialize(3);
        ind[0] = 20;
        Assert.Equal(20, ind[0]);
        ind.Dispose();
    }

    [Fact]
    public void Calculate_After_Dispose_Does_Not_Throw()
    {
        var ind = new TestableIndicator();
        ind.Initialize(2);
        ind.Dispose();
        ind.Calculate(0);
    }

    [Fact]
    public void SetWindow_Sets_Window_Property()
    {
        using var ind = new TestableIndicator();
        using var window = new TickWindow(SymbolsX, TimeframeM1);
        ((IWindowAwareIndicator)ind).SetWindow(window);
    }
}
