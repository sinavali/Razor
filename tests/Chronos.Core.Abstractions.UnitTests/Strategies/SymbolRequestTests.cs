using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;
using System.Collections.Immutable;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

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
        var modified = sr with { Symbol = "ETHUSDT" };
        Assert.Equal("BTCUSDT", sr.Symbol);
        Assert.Equal("ETHUSDT", modified.Symbol);
    }

    [Fact]
    public void Equality()
    {
        // ImmutableArray<T> uses reference equality, not structural equality, so two instances with
        // identical elements may not be equal as records. We verify field equality instead.
        var a = new SymbolRequest("X", ImmutableArray.Create(TimeFrame.M1));
        var b = new SymbolRequest("X", ImmutableArray.Create(TimeFrame.M1));
        Assert.Equal(a.Symbol, b.Symbol);
        Assert.True(a.TimeFrames.SequenceEqual(b.TimeFrames));
        // The records themselves are not equal due to ImmutableArray's reference semantics.
        Assert.NotEqual(a, b);
    }
}
