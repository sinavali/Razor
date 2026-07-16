using Razor.Core.Sdk.Shared;
using System.Collections.Immutable;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class SymbolRequestTests
{
    [Fact]
    public void Default_Constructor_Empty()
    {
        var sr = new SymbolRequest();
        Assert.Equal(string.Empty, sr.Symbol);
        Assert.True(sr.TimeFrames.IsEmpty);
    }

    [Fact]
    public void Constructor_With_Values()
    {
        var sr = new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1, TimeFrame.H1));
        Assert.Equal("EURUSD", sr.Symbol);
        Assert.Equal(2, sr.TimeFrames.Length);
    }

    [Fact]
    public void With_Changes()
    {
        var sr = new SymbolRequest("BTCUSDT", ImmutableArray.Create(TimeFrame.M5));
        var modified = sr with
        {
            Symbol = "ETHUSDT"
        };
        Assert.Equal("BTCUSDT", sr.Symbol);
        Assert.Equal("ETHUSDT", modified.Symbol);
    }

    [Fact]
    public void Field_Equality_Not_Record_Equality()
    {
        var a = new SymbolRequest("X", ImmutableArray.Create(TimeFrame.M1));
        var b = new SymbolRequest("X", ImmutableArray.Create(TimeFrame.M1));
        Assert.Equal(a.Symbol, b.Symbol);
        Assert.True(a.TimeFrames.SequenceEqual(b.TimeFrames));
        Assert.NotEqual(a, b); // ImmutableArray uses reference equality
    }
}
