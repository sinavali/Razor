using Razor.Core.Sdk.Hooks;

namespace Razor.Core.Sdk.UnitTests.Hooks;

public class FilterResultTests
{
    [Fact]
    public void Allow_Returns_Allowed_With_Data()
    {
        var res = FilterResult.Allow(42);
        Assert.True(res.IsAllowed);
        Assert.Equal(42, res.Data);
        Assert.Null(res.RejectionReason);
    }

    [Fact]
    public void Reject_Returns_NotAllowed_With_Reason()
    {
        var res = FilterResult.Reject<int>("test reason");
        Assert.False(res.IsAllowed);
        // For value types, rejected data is default(T) – here int defaults to 0.
        Assert.Equal(0, res.Data);
        Assert.Equal("test reason", res.RejectionReason);
    }

    [Fact]
    public void Allow_Null_Data_Allowed()
    {
        var res = FilterResult.Allow<string?>(null);
        Assert.True(res.IsAllowed);
        Assert.Null(res.Data);
    }

    [Fact]
    public void Equality_Same_Allowed()
    {
        var a = FilterResult.Allow("hello");
        var b = FilterResult.Allow("hello");
        Assert.True(a == b);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_Different_Rejection_Reasons()
    {
        var a = FilterResult.Reject<string>("reason A");
        var b = FilterResult.Reject<string>("reason B");
        Assert.False(a == b);
    }

    [Fact]
    public void Equality_One_Allowed_One_Rejected()
    {
        var a = FilterResult.Allow(1);
        var b = FilterResult.Reject<int>("nope");
        Assert.False(a == b);
    }

    [Fact]
    public void GetHashCode_Stable()
    {
        var res = FilterResult.Allow(10);
        Assert.Equal(HashCode.Combine(true, 10, (string?)null), res.GetHashCode());
    }
}
