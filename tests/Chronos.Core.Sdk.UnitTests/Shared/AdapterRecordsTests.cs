using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.UnitTests.Shared;

public class AdapterOrderRequestTests
{
    [Fact]
    public void Default_Values_Are_Set()
    {
        var r = new AdapterOrderRequest();
        Assert.Null(r.MagicNumber);
        Assert.Equal(string.Empty, r.Symbol);
        Assert.Equal(OrderType.Buy, r.Type);
        Assert.Equal(0.0, r.Volume);
        Assert.Equal(0.0, r.Price);
        Assert.Equal(0.0, r.StopLoss);
        Assert.Equal(0.0, r.TakeProfit);
        Assert.Equal(string.Empty, r.Comment);
    }

    [Fact]
    public void Equality_Works()
    {
        var a = new AdapterOrderRequest { Symbol = "EURUSD", Volume = 0.1 };
        var b = new AdapterOrderRequest { Symbol = "EURUSD", Volume = 0.1 };
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Different_Values_Not_Equal()
    {
        var a = new AdapterOrderRequest { Symbol = "EURUSD" };
        var b = new AdapterOrderRequest { Symbol = "GBPUSD" };
        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void With_Changes_Correctly()
    {
        var original = new AdapterOrderRequest { Symbol = "BTCUSDT", Volume = 1.0 };
        var modified = original with
        {
            Volume = 1.5
        };
        Assert.Equal("BTCUSDT", modified.Symbol);
        Assert.Equal(1.5, modified.Volume);
        Assert.Equal(original.Symbol, modified.Symbol);
    }

    [Fact]
    public void MagicNumber_Optional()
    {
        var r = new AdapterOrderRequest { MagicNumber = 42 };
        Assert.Equal(42, r.MagicNumber);
    }
}

public class ExecutionReportTests
{
    [Fact]
    public void Default_Values()
    {
        var r = new ExecutionReport();
        Assert.Equal(0L, r.Ticket);
        Assert.Equal(string.Empty, r.Symbol);
        Assert.Equal(OrderType.Buy, r.Type);
        Assert.Equal(ExecutionState.New, r.State);
        Assert.Equal(0.0, r.ExecutedVolume);
        Assert.Equal(0.0, r.ExecutedPrice);
        Assert.Equal(0.0, r.RemainingVolume);
        Assert.Equal(0.0, r.Commission);
        Assert.Equal(0.0, r.RealizedPnL);
        Assert.Equal(0L, r.Timestamp);
        Assert.Equal(string.Empty, r.Comment);
    }

    [Fact]
    public void Fully_Populated_Report_Equals()
    {
        var a = new ExecutionReport
        {
            Ticket = 1,
            Symbol = "X",
            Type = OrderType.Sell,
            State = ExecutionState.Filled,
            ExecutedVolume = 0.5,
            ExecutedPrice = 1.2,
            RemainingVolume = 0.3,
            Commission = 0.01,
            RealizedPnL = 5.0,
            Timestamp = 100,
            Comment = "c"
        };
        var b = a with
        {
        };
        Assert.Equal(a, b);
    }

    [Fact]
    public void NotEqual_When_Different()
    {
        var a = new ExecutionReport { Ticket = 1 };
        var b = new ExecutionReport { Ticket = 2 };
        Assert.NotEqual(a, b);
    }
}

public class HistoricalDataRequestTests
{
    [Fact]
    public void Default_Values()
    {
        var r = new HistoricalDataRequest();
        Assert.Equal(string.Empty, r.Symbol);
        Assert.Equal(default(DateTime), r.StartTime);
        Assert.Equal(default(DateTime), r.EndTime);
        Assert.Equal(DataActionPolicy.KeepUntilExit, r.RetentionPolicy);
    }

    [Fact]
    public void Setting_Retention_Policy()
    {
        var r = new HistoricalDataRequest { RetentionPolicy = DataActionPolicy.PersistentCache };
        Assert.Equal(DataActionPolicy.PersistentCache, r.RetentionPolicy);
    }
}

public class HistoricalDataResponseTests
{
    [Fact]
    public void Default_Failure()
    {
        var r = new HistoricalDataResponse();
        Assert.Equal(string.Empty, r.Symbol);
        Assert.False(r.Success);
        Assert.Equal(string.Empty, r.ErrorMessage);
        Assert.Equal(string.Empty, r.BinaryFilePath);
        Assert.Equal(0L, r.TotalRecords);
    }

    [Fact]
    public void Success_With_Data()
    {
        var r = new HistoricalDataResponse
        {
            Symbol = "EURUSD",
            Success = true,
            BinaryFilePath = "/path",
            TotalRecords = 1000
        };
        Assert.True(r.Success);
        Assert.Equal(1000L, r.TotalRecords);
    }
}
