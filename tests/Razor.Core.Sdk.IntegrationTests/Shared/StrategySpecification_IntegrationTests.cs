using Razor.Core.Sdk.Shared;
using System.Collections.Immutable;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class StrategySpecification_IntegrationTests
{
    [Fact]
    public void Create_And_Validate_With_100_Symbols()
    {
        var symbols = new SymbolRequest[100];
        for (int i = 0; i < 100; i++)
        {
            symbols[i] = new SymbolRequest($"SYM{i:D3}", ImmutableArray.Create(TimeFrame.M1, TimeFrame.H1));
        }

        var spec = StrategySpecification.CreateValidated(100000, 50, ImmutableArray.Create(symbols));
        Assert.Equal(100000, spec.InitialBalance);
        Assert.Equal(50, spec.Leverage);
        Assert.Equal(100, spec.RequestedSymbols.Length);
    }

    [Fact]
    public void CreateValidated_Rejects_Duplicate_In_Large_Set()
    {
        var symbols = new SymbolRequest[50];
        for (int i = 0; i < 50; i++)
        {
            symbols[i] = new SymbolRequest($"SYM{i:D3}", ImmutableArray.Create(TimeFrame.M1));
        }

        symbols[49] = new SymbolRequest("SYM000", ImmutableArray.Create(TimeFrame.H1));

        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10, ImmutableArray.Create(symbols)));
    }

    [Fact]
    public void CreateValidated_Rejects_Whitespace_Symbol()
    {
        Assert.Throws<ConfigurationException>(() =>
            StrategySpecification.CreateValidated(1000, 10,
                System.Collections.Immutable.ImmutableArray.Create(
                    new SymbolRequest("   ", System.Collections.Immutable.ImmutableArray.Create(TimeFrame.M1)))));
    }

    [Fact]
    public void Validate_Method_Throws_On_Invalid_Instance()
    {
        var spec = new StrategySpecification
        {
            InitialBalance = -100,
            Leverage = 1,
            RequestedSymbols = System.Collections.Immutable.ImmutableArray.Create(
                new SymbolRequest("EURUSD", System.Collections.Immutable.ImmutableArray.Create(TimeFrame.M1)))
        };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }

    [Fact]
    public void Validate_Method_Throws_On_Invalid_Leverage()
    {
        var spec = new StrategySpecification
        {
            InitialBalance = 1000,
            Leverage = -1,
            RequestedSymbols = System.Collections.Immutable.ImmutableArray.Create(
                new SymbolRequest("EURUSD", System.Collections.Immutable.ImmutableArray.Create(TimeFrame.M1)))
        };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }
}
