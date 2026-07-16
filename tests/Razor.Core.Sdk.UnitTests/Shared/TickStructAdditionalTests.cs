using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class TickStructAdditionalTests
{
    [Fact]
    public void Equals_Boxed_Tick_Returns_True()
    {
        var a = new Tick(1, 2, 3, 4, true);
        object b = new Tick(1, 2, 3, 4, true);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_Null_Returns_False()
    {
        var a = new Tick(1, 2, 3, 4);
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void Equals_Different_Type_Returns_False()
    {
        var a = new Tick(1, 2, 3, 4);
        Assert.False(a.Equals("not a tick"));
    }

    [Fact]
    public void NotEqual_Operator_Works()
    {
        var a = new Tick(1, 2, 3, 4);
        var b = new Tick(2, 2, 3, 4);
        Assert.True(a != b);
    }

    [Fact]
    public void NotEqual_Operator_Returns_False_For_Same()
    {
        var a = new Tick(1, 2, 3, 4);
        var b = a;
        Assert.False(a != b);
    }
}
