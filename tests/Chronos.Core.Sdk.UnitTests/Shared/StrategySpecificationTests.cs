using Chronos.Core.Sdk.Shared;
using System.Collections.Immutable;

namespace Chronos.Core.Sdk.UnitTests.Shared;

public class StrategySpecificationTests
{
    [Fact]
    public void CreateValidated_Valid_Succeeds()
    {
        var spec = StrategySpecification.CreateValidated(1000, 10,
            ImmutableArray.Create(new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1))));
        Assert.Equal(1000, spec.InitialBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InitialBalance_NonPositive_Throws(double balance) =>
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(balance, 10,
                ImmutableArray.Create(new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1)))));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Leverage_NonPositive_Throws(double leverage) =>
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, leverage,
                ImmutableArray.Create(new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1)))));

    [Fact]
    public void Empty_RequestedSymbols_Throws() =>
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10, ImmutableArray<SymbolRequest>.Empty));

    [Fact]
    public void SymbolRequest_Empty_Symbol_Throws()
    {
        var sr = new SymbolRequest(" ", ImmutableArray.Create(TimeFrame.M1));
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(sr)));
    }

    [Fact]
    public void SymbolRequest_No_Timeframes_Throws()
    {
        var sr = new SymbolRequest("EURUSD", ImmutableArray<TimeFrame>.Empty);
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(sr)));
    }

    [Fact]
    public void Duplicate_Symbols_Throws()
    {
        var sr1 = new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1));
        var sr2 = new SymbolRequest("eurusd", ImmutableArray.Create(TimeFrame.H1));
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(sr1, sr2)));
    }

    [Fact]
    public void Distinct_Symbols_Allowed()
    {
        var spec = StrategySpecification.CreateValidated(1000, 10,
            ImmutableArray.Create(
                new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1)),
                new SymbolRequest("GBPUSD", ImmutableArray.Create(TimeFrame.M1))));
        Assert.Equal(2, spec.RequestedSymbols.Length);
    }
}
