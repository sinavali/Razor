using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;
using System.Collections.Immutable;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

public class StrategySpecificationTests
{
    private SymbolRequest ValidSymbol() => new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1));

    [Fact]
    public void CreateValidated_With_Valid_Data_Succeeds()
    {
        var spec = StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(ValidSymbol()));
        Assert.Equal(1000, spec.InitialBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InitialBalance_NonPositive_Throws(double balance)
    {
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(balance, 10, ImmutableArray.Create(ValidSymbol())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Leverage_NonPositive_Throws(double leverage)
    {
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, leverage, ImmutableArray.Create(ValidSymbol())));
    }

    [Fact]
    public void Empty_RequestedSymbols_Throws()
    {
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10, ImmutableArray<SymbolRequest>.Empty));
    }

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
    public void FrictionModel_Null_Allowed()
    {
        var spec = StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(ValidSymbol()), null, null);
        Assert.Null(spec.FrictionModel);
    }

    [Fact]
    public void FitnessModel_Null_Allowed()
    {
        var spec = StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(ValidSymbol()));
        Assert.Null(spec.FitnessModel);
    }
}
