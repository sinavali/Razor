using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class AdapterOrderResponseTests
{
    [Fact]
    public void Default_Failure_State()
    {
        var r = new AdapterOrderResponse();
        Assert.False(r.Success);
        Assert.Equal(string.Empty, r.ErrorMessage);
        Assert.Equal(0L, r.Ticket);
        Assert.Equal(0.0, r.ExecutedPrice);
        Assert.Equal(0.0, r.ExecutedVolume);
    }
}

public class BarTests
{
    [Fact]
    public void Equality_And_Hashing()
    {
        var b1 = new Bar(100, 1.2, 1.5, 1.1, 1.3, 1000, 200);
        var b2 = new Bar(100, 1.2, 1.5, 1.1, 1.3, 1000, 200);
        var b3 = new Bar(101, 1.2, 1.5, 1.1, 1.3, 1000, 200);
        Assert.True(b1.Equals(b2));
        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
        Assert.False(b1.Equals(b3));
        Assert.False(b1.Equals(null));
    }

    [Fact]
    public void Operators()
    {
        var b1 = new Bar(1, 1, 1, 1, 1, 1, 1);
        var b2 = b1;
        Assert.True(b1 == b2);
        Assert.False(b1 != b2);
    }
}

public class OrderTests
{
    [Fact]
    public void Default_Values()
    {
        var o = new Order();
        Assert.Equal(0L, o.Ticket);
        Assert.Equal(string.Empty, o.Symbol);
        Assert.Equal(OrderType.Buy, o.Type);
        Assert.Equal(0.0, o.Volume);
        Assert.Equal(0.0, o.Price);
        Assert.Equal(0.0, o.SL);
        Assert.Equal(0.0, o.TP);
        Assert.Equal(string.Empty, o.Comment);
    }

    [Fact]
    public void Immutability_With()
    {
        var o = new Order { Ticket = 123, Symbol = "EURUSD" };
        var copy = o with { Ticket = 456 };
        Assert.Equal(123, o.Ticket);
        Assert.Equal(456, copy.Ticket);
    }

    [Fact]
    public void Equality()
    {
        var a = new Order { Ticket = 1 };
        var b = new Order { Ticket = 1 };
        Assert.Equal(a, b);
    }
}

public class PositionTests
{
    [Fact]
    public void Default_Values()
    {
        var p = new Position();
        Assert.Equal(0L, p.Ticket);
        Assert.Equal(string.Empty, p.Symbol);
        Assert.Equal(OrderType.Buy, p.Type);
        Assert.Equal(0.0, p.Volume);
        Assert.Equal(0.0, p.OpenPrice);
        Assert.Equal(0L, p.OpenTime);
        Assert.Equal(0.0, p.ClosePrice);
        Assert.Equal(0L, p.CloseTime);
        Assert.Equal(0.0, p.SL);
        Assert.Equal(0.0, p.TP);
        Assert.Equal(0.0, p.Commission);
        Assert.Equal(0.0, p.Swap);
        Assert.Equal(0.0, p.Profit);
        Assert.Equal(0.0, p.ReturnPct);
        Assert.Equal(0.0, p.AccountEquityAtOpen);
        Assert.Equal(0.0, p.Leverage);
        Assert.False(p.IsMargin);
        Assert.Equal(string.Empty, p.Comment);
        Assert.False(p.IsClosed); // CloseTime == 0
    }

    [Fact]
    public void IsClosed_When_CloseTime_Greater_Than_Zero()
    {
        var p = new Position { CloseTime = 100 };
        Assert.True(p.IsClosed);
    }

    [Fact]
    public void With_Changes()
    {
        var p = new Position { Ticket = 1 };
        var modified = p with { Volume = 2.0 };
        Assert.Equal(1L, p.Ticket);
        Assert.Equal(2.0, modified.Volume);
    }
}

public class TickTests
{
    [Fact]
    public void Constructor_Defaults()
    {
        var t = new Tick(0, 0, 0, 0);
        Assert.False(t.IsSynthetic);
        Assert.Equal(0L, t.Time);
    }

    [Fact]
    public void Equality_Same_Values()
    {
        var a = new Tick(1, 2, 3, 4, true);
        var b = new Tick(1, 2, 3, 4, true);
        Assert.True(a.Equals(b));
        Assert.True(a == b);
    }

    [Fact]
    public void NotEqual_When_Different()
    {
        var a = new Tick(1, 2, 3, 4);
        var b = new Tick(2, 2, 3, 4);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashCode_Stable()
    {
        var t = new Tick(100, 1.5, 1.6, 10, true);
        Assert.Equal(HashCode.Combine(100L, 1.5, 1.6, 10.0, true), t.GetHashCode());
    }
}

public class SymbolPropertiesTests
{
    private SymbolProperties CreateDefault() => new SymbolProperties
    {
        AssetClass = AssetClass.Forex,
        MarginMode = MarginMode.Cross,
        PendingTrigger = PendingOrderTriggerMode.UseAskForBuy,
        MarginCurrency = "USD",
        ContractSize = 1,
        TickSize = 0.01,
        TickValue = 1,
        MinVolume = 0.01,
        MaxLeverage = 50,
        SwapLong = 0,
        SwapShort = 0,
        SwapRolloverHourUtc = 0,
        TripleSwapDayMultiplier = 1,
        FundingRate = 0,
        InitialMarginRate = 1,
        MaintenanceMarginRate = 0.5,
        MakerFeeRate = 0.001,
        TakerFeeRate = 0.002
    };

    [Fact]
    public void Can_Create_With_Required_Properties()
    {
        var props = CreateDefault();
        Assert.Equal("USD", props.MarginCurrency);
    }

    [Fact]
    public void Equality()
    {
        var a = CreateDefault();
        var b = CreateDefault();
        Assert.Equal(a, b);
    }

    [Fact]
    public void With_Change()
    {
        var a = CreateDefault();
        var b = a with { TickSize = 0.05 };
        Assert.NotEqual(a, b);
    }
}
