using Razor.Core.Sdk.Shared;
using System.Collections.Immutable;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class SymbolRequest_IntegrationTests
{
    [Fact]
    public void Constructor_With_Symbol_And_Timeframes()
    {
        var sr = new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1, TimeFrame.H1));
        Assert.Equal("EURUSD", sr.Symbol);
        Assert.Equal(2, sr.TimeFrames.Length);
    }

    [Fact]
    public void Default_Constructor_Produces_Empty()
    {
        var sr = new SymbolRequest();
        Assert.Equal(string.Empty, sr.Symbol);
        Assert.True(sr.TimeFrames.IsEmpty);
    }

    [Fact]
    public void With_Expression_Works()
    {
        var sr = new SymbolRequest("BTCUSDT", ImmutableArray.Create(TimeFrame.M5));
        var modified = sr with
        {
            Symbol = "ETHUSDT"
        };
        Assert.Equal("BTCUSDT", sr.Symbol);
        Assert.Equal("ETHUSDT", modified.Symbol);
    }
}
