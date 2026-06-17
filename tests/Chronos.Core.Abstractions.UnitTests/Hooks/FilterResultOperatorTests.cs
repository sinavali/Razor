using Chronos.Core.Abstractions.Hooks;

namespace Chronos.Core.Abstractions.UnitTests.Hooks;

public class FilterResultOperatorTests
{
    [Fact]
    public void NotEqual_Operator_Allowed_Different_Data()
    {
        var a = FilterResult.Allow(10);
        var b = FilterResult.Allow(20);
        Assert.True(a != b);
    }

    [Fact]
    public void NotEqual_Operator_Same_Data()
    {
        var a = FilterResult.Allow(10);
        var b = FilterResult.Allow(10);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_Object_Allowed_True()
    {
        var a = FilterResult.Allow(10);
        object b = FilterResult.Allow(10);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_Object_Rejected_True()
    {
        var a = FilterResult.Reject<int>("reason");
        object b = FilterResult.Reject<int>("reason");
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_Object_WrongType_Returns_False()
    {
        var a = FilterResult.Allow(10);
        Assert.False(a.Equals("not a filter result"));
    }

    [Fact]
    public void Equals_Object_Null_Returns_False()
    {
        var a = FilterResult.Allow(10);
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void GetHashCode_Same_For_Equal_Instances()
    {
        var a = FilterResult.Allow(10);
        var b = FilterResult.Allow(10);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
