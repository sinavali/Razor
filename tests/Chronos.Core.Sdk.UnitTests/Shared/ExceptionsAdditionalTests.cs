using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.UnitTests.Shared;

public class ExceptionsAdditionalTests
{
    [Fact]
    public void StrategyException_With_Inner_Exception()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new StrategyException("Strat1", "bad", inner);
        Assert.Equal(inner, ex.InnerException);
        Assert.Contains("[Strat1]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OptimizationException_With_Inner_Exception()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new OptimizationException("opt error", inner);
        Assert.Equal(inner, ex.InnerException);
        Assert.Equal("opt error", ex.Message);
    }

    [Fact]
    public void AdapterException_Default_Message_Contains_AdapterName()
    {
        var ex = new AdapterException("MyAdapter", "some error");
        Assert.Contains("[MyAdapter]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppException_Protected_Constructor_Can_Be_Instantiated_Via_Derived()
    {
        var ex = new StrategyException("S", "test");
        Assert.IsAssignableFrom<AppException>(ex);
    }
}
