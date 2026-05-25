using System.Buffers;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

internal sealed class TestableIndicator : Indicator
{
    public override void Calculate(long index) { }
}

public class IndicatorTests
{
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
}

public class IndicatorEdgeCaseTests
{
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

        // Should not crash; the base class doesn't enforce buffer validity in Calculate.
        ind.Calculate(0);
    }
}
