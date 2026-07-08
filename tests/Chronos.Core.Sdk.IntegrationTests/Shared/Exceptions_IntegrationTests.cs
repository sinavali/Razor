using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.IntegrationTests.Shared;

public class Exceptions_IntegrationTests
{
    [Fact]
    public void AdapterException_Full_Context()
    {
        var ex = new AdapterException("BinanceAdapter", "Connection refused");
        Assert.Contains("[BinanceAdapter]", ex.Message, StringComparison.Ordinal);
        Assert.Equal("BinanceAdapter", ex.AdapterName);
        Assert.IsAssignableFrom<AppException>(ex);

        var inner = new InvalidOperationException("timeout");
        var exWithInner = new AdapterException("BinanceAdapter", "Connection refused", inner);
        Assert.Equal(inner, exWithInner.InnerException);
    }

    [Fact]
    public void StrategyException_Full_Context()
    {
        var ex = new StrategyException("MACrossover", "Invalid parameter");
        Assert.Contains("[MACrossover]", ex.Message, StringComparison.Ordinal);
        Assert.Equal("MACrossover", ex.StrategyName);
        Assert.IsAssignableFrom<AppException>(ex);

        var inner = new ArgumentException("bad value");
        var exWithInner = new StrategyException("MACrossover", "Invalid parameter", inner);
        Assert.Equal(inner, exWithInner.InnerException);
    }

    [Fact]
    public void OptimizationException_Constructors()
    {
        var ex1 = new OptimizationException();
        Assert.IsAssignableFrom<AppException>(ex1);

        var ex2 = new OptimizationException("convergence failure");
        Assert.Equal("convergence failure", ex2.Message);

        var inner = new InvalidOperationException("stagnation");
        var ex3 = new OptimizationException("convergence failure", inner);
        Assert.Equal(inner, ex3.InnerException);
    }

    [Fact]
    public void ConfigurationException_Message_And_Inner()
    {
        var ex = new ConfigurationException("Invalid value");
        Assert.Equal("Invalid value", ex.Message);
        Assert.IsAssignableFrom<Exception>(ex);

        var inner = new ArgumentException("details");
        var exWithInner = new ConfigurationException("Invalid value", inner);
        Assert.Equal(inner, exWithInner.InnerException);
    }

    [Fact]
    public void AppException_Hierarchy()
    {
        Assert.IsAssignableFrom<AppException>(new AdapterException("A", "test"));
        Assert.IsAssignableFrom<AppException>(new StrategyException("S", "test"));
        Assert.IsAssignableFrom<AppException>(new OptimizationException("test"));
    }
}
