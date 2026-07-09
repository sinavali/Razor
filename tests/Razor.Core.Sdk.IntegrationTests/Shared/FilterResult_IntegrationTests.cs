using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.IntegrationTests.Hooks;

public class FilterResult_IntegrationTests
{
    [Fact]
    public void Filter_Many_Items_In_Pipeline()
    {
        int allowed = 0;
        int rejected = 0;

        for (int i = 0; i < 1000; i++)
        {
            var result = i % 3 == 0
                ? FilterResult.Reject<int>($"reject {i}")
                : FilterResult.Allow(i);

            if (result.IsAllowed)
            {
                allowed++;
            }
            else
            {
                rejected++;
            }
        }

        Assert.Equal(666, allowed);
        Assert.Equal(334, rejected);
    }

    [Fact]
    public void Large_Data_Equality_Check()
    {
        var a = FilterResult.Allow(new string('x', 10000));
        var b = FilterResult.Allow(new string('x', 10000));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Filter_One_Million_Items()
    {
        int allowed = 0;
        int rejected = 0;
        for (int i = 0; i < 1_000_000; i++)
        {
            var res = i % 7 == 0 ? FilterResult.Reject<int>($"r{i}") : FilterResult.Allow(i);
            if (res.IsAllowed)
            {
                allowed++;
            }
            else
            {
                rejected++;
            }
        }

        Assert.Equal(142_858, rejected);
        Assert.Equal(857_142, allowed);
    }

    [Fact]
    public void FilterResult_Allow_And_Reject_With_Value_Types()
    {
        var allowed = FilterResult.Allow(42);
        Assert.True(allowed.IsAllowed);
        Assert.Equal(42, allowed.Data);
        Assert.Null(allowed.RejectionReason);

        var rejected = FilterResult.Reject<int>("bad input");
        Assert.False(rejected.IsAllowed);
        // For value types, rejected data is default(T), here int defaults to 0.
        Assert.Equal(0, rejected.Data);
        Assert.Equal("bad input", rejected.RejectionReason);
    }

    [Fact]
    public void FilterResult_Allow_And_Reject_With_Reference_Types()
    {
        var allowed = FilterResult.Allow("hello");
        Assert.True(allowed.IsAllowed);
        Assert.Equal("hello", allowed.Data);

        var rejected = FilterResult.Reject<string>("invalid");
        Assert.False(rejected.IsAllowed);
        Assert.Null(rejected.Data);
        Assert.Equal("invalid", rejected.RejectionReason);
    }

    [Fact]
    public void FilterResult_Equality_Checks()
    {
        var a = FilterResult.Allow(10);
        var b = FilterResult.Allow(10);
        var c = FilterResult.Allow(20);
        var d = FilterResult.Reject<int>("reason");

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(d));
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void FilterResult_Equals_Null_Returns_False()
    {
        var a = FilterResult.Allow(10);
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void FilterResult_NotEqual_Operator()
    {
        var a = FilterResult.Allow(1);
        var b = FilterResult.Allow(2);
        Assert.True(a != b);
    }

    [Fact]
    public void FilterResult_Custom_Type()
    {
        var data = new Position { Ticket = 1, Symbol = "EURUSD" };
        var allowed = FilterResult.Allow(data);
        Assert.True(allowed.IsAllowed);
        Assert.Equal(data, allowed.Data);
    }
}
