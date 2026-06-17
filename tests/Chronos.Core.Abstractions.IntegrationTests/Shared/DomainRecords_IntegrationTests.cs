using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class DomainRecords_IntegrationTests
{
    [Fact]
    public void AdapterOrderRequest_All_Fields_Roundtrip()
    {
        var req = new AdapterOrderRequest
        {
            MagicNumber = 42,
            Symbol = "EURUSD",
            Type = OrderType.BuyLimit,
            Volume = 0.5,
            Price = 1.2345,
            StopLoss = 1.2200,
            TakeProfit = 1.2500,
            Comment = "test order"
        };

        Assert.Equal(42, req.MagicNumber);
        Assert.Equal("EURUSD", req.Symbol);
        Assert.Equal(OrderType.BuyLimit, req.Type);
        Assert.Equal(0.5, req.Volume);
        Assert.Equal(1.2345, req.Price);
        Assert.Equal(1.2200, req.StopLoss);
        Assert.Equal(1.2500, req.TakeProfit);
        Assert.Equal("test order", req.Comment);

        var copy = req with { Volume = 1.0 };
        Assert.Equal(1.0, copy.Volume);
        Assert.Equal(0.5, req.Volume);
        Assert.Equal(req, req);
        Assert.Equal(copy, copy);
        Assert.NotEqual(req, copy);
    }

    [Fact]
    public void AdapterOrderResponse_All_Fields_Roundtrip()
    {
        var resp = new AdapterOrderResponse
        {
            Success = true,
            ErrorMessage = "none",
            Ticket = 12345,
            ExecutedPrice = 1.2345,
            ExecutedVolume = 0.5
        };

        Assert.True(resp.Success);
        Assert.Equal("none", resp.ErrorMessage);
        Assert.Equal(12345, resp.Ticket);
        Assert.Equal(1.2345, resp.ExecutedPrice);
        Assert.Equal(0.5, resp.ExecutedVolume);

        var failed = new AdapterOrderResponse { Success = false, ErrorMessage = "rejected" };
        Assert.False(failed.Success);
        Assert.Equal("rejected", failed.ErrorMessage);
        Assert.Equal(0L, failed.Ticket);
    }

    [Fact]
    public void ExecutionReport_All_States_And_Fields()
    {
        var report = new ExecutionReport
        {
            Ticket = 100,
            Symbol = "BTCUSDT",
            Type = OrderType.Sell,
            State = ExecutionState.Filled,
            ExecutedVolume = 1.5,
            ExecutedPrice = 50000,
            RemainingVolume = 0,
            Commission = 0.75,
            RealizedPnL = 150,
            Timestamp = 1234567890,
            Comment = "filled order"
        };

        Assert.Equal(100, report.Ticket);
        Assert.Equal(ExecutionState.Filled, report.State);
        Assert.Equal(1.5, report.ExecutedVolume);
        Assert.Equal(50000, report.ExecutedPrice);
        Assert.Equal(0.75, report.Commission);
        Assert.Equal(150, report.RealizedPnL);

        var copy = report with { State = ExecutionState.New };
        Assert.Equal(ExecutionState.Filled, report.State);
        Assert.Equal(ExecutionState.New, copy.State);
    }

    [Fact]
    public void HistoricalDataRequest_And_Response_Roundtrip()
    {
        var req = new HistoricalDataRequest
        {
            Symbol = "EURUSD",
            StartTime = new DateTime(2024, 1, 1),
            EndTime = new DateTime(2024, 12, 31),
            RetentionPolicy = DataActionPolicy.PersistentCache
        };

        Assert.Equal("EURUSD", req.Symbol);
        Assert.Equal(new DateTime(2024, 1, 1), req.StartTime);
        Assert.Equal(DataActionPolicy.PersistentCache, req.RetentionPolicy);

        var resp = new HistoricalDataResponse
        {
            Symbol = "EURUSD",
            Success = true,
            BinaryFilePath = "/data/eurusd.chrs",
            TotalRecords = 500000
        };

        Assert.True(resp.Success);
        Assert.Equal("/data/eurusd.chrs", resp.BinaryFilePath);
        Assert.Equal(500000, resp.TotalRecords);
    }

    [Fact]
    public void Order_Record_Roundtrip()
    {
        var order = new Order
        {
            Ticket = 999,
            Symbol = "GBPUSD",
            Type = OrderType.SellStop,
            Volume = 0.3,
            Price = 1.2800,
            SL = 1.2900,
            TP = 1.2500,
            Comment = "stop order"
        };

        Assert.Equal(999, order.Ticket);
        Assert.Equal(OrderType.SellStop, order.Type);
        Assert.Equal(1.2800, order.Price);

        var copy = order with { Volume = 0.5 };
        Assert.Equal(0.5, copy.Volume);
        Assert.Equal(0.3, order.Volume);
    }

    [Fact]
    public void Position_Open_And_Closed_States()
    {
        var openPos = new Position
        {
            Ticket = 100,
            Symbol = "USDJPY",
            Type = OrderType.Buy,
            Volume = 1.0,
            OpenPrice = 150.00,
            OpenTime = 1000,
            SL = 149.00,
            TP = 152.00,
            Commission = 0.5,
            Swap = 0.1,
            Profit = 10.0,
            ReturnPct = 1.5,
            AccountEquityAtOpen = 10000,
            Leverage = 10,
            IsMargin = true,
            Comment = "long trade"
        };

        Assert.False(openPos.IsClosed);
        Assert.Equal(150.00, openPos.OpenPrice);
        Assert.Equal(10.0, openPos.Profit);

        var closedPos = openPos with { CloseTime = 2000, ClosePrice = 151.00 };
        Assert.True(closedPos.IsClosed);
        Assert.Equal(151.00, closedPos.ClosePrice);
    }

    [Fact]
    public void StrategySpecification_Validation_Large_Symbol_Count()
    {
        var symbols = Enumerable.Range(0, 50).Select(i =>
            new SymbolRequest($"SYM{i:D4}", System.Collections.Immutable.ImmutableArray.Create(TimeFrame.M1, TimeFrame.H1))
        ).ToArray();

        var spec = StrategySpecification.CreateValidated(50000, 100,
            System.Collections.Immutable.ImmutableArray.Create(symbols));

        Assert.Equal(50000, spec.InitialBalance);
        Assert.Equal(100, spec.Leverage);
        Assert.Equal(50, spec.RequestedSymbols.Length);
    }
}
