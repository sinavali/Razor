using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.UnitTests.Shared;

public class ExceptionsTests
{
    [Fact]
    public void ConfigurationException_Message()
    {
        var ex = new ConfigurationException("test");
        Assert.Equal("test", ex.Message);
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void ConfigurationException_With_Inner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ConfigurationException("test", inner);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void AdapterException_Includes_AdapterName()
    {
        var ex = new AdapterException("MyAdapter", "Error");
        Assert.Contains("[MyAdapter]", ex.Message, StringComparison.Ordinal);
        Assert.Equal("MyAdapter", ex.AdapterName);
    }

    [Fact]
    public void AdapterException_With_Inner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new AdapterException("A", "msg", inner);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void StrategyException_Includes_Name()
    {
        var ex = new StrategyException("Strat1", "bad");
        Assert.Contains("[Strat1]", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Strat1", ex.StrategyName);
    }

    [Fact]
    public void OptimizationException_Empty_Constructor()
    {
        var ex = new OptimizationException();
        Assert.IsAssignableFrom<AppException>(ex);
    }

    [Fact]
    public void OptimizationException_With_Message()
    {
        var ex = new OptimizationException("opt error");
        Assert.Equal("opt error", ex.Message);
    }

    [Fact]
    public void AppException_Is_Abstract() => Assert.True(typeof(AppException).IsAbstract);
}
