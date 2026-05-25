using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Shared.Events;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class EventsTests
{
    [Fact]
    public void BacktestStartedEvent_Defaults()
    {
        var evt = new BacktestStartedEvent();
        Assert.NotEqual(default(DateTime), evt.Timestamp);
        Assert.Null(evt.CorrelationId);
        Assert.Null(evt.EventId);
    }

    [Fact]
    public void BacktestStartedEvent_Can_Set_Ids()
    {
        var id = Guid.NewGuid();
        var evt = new BacktestStartedEvent { CorrelationId = id, EventId = "abc" };
        Assert.Equal(id, evt.CorrelationId);
        Assert.Equal("abc", evt.EventId);
    }

    [Fact]
    public void BacktestCompletedEvent_Stores_All_Metrics()
    {
        var evt = new BacktestCompletedEvent
        {
            NetProfit = 500.0,
            ReturnPct = 10.5,
            MaxDrawdownPct = 15.2,
            MaxDailyDrawdownPct = 8.1,
            TotalTrades = 42,
            WinRatePct = 62.3,
            ProfitFactor = 1.8,
            SharpeRatio = 0.9,
            SortinoRatio = 1.2
        };
        Assert.Equal(500.0, evt.NetProfit);
        Assert.Equal(42, evt.TotalTrades);
        Assert.Equal(0.9, evt.SharpeRatio);
    }

    [Fact]
    public void ConnectionStateEvent_Stores_State()
    {
        var evt = new ConnectionStateEvent(true, "MyAdapter");
        Assert.True(evt.IsConnected);
        Assert.Equal("MyAdapter", evt.AdapterName);
    }

    [Fact]
    public void LiveReconnectEvent_Stores_Values()
    {
        var evt = new LiveReconnectEvent(false, 3, "A");
        Assert.False(evt.Success);
        Assert.Equal(3, evt.AttemptCount);
    }

    [Fact]
    public void LiveSessionEndedEvent_Stores_Metrics()
    {
        var evt = new LiveSessionEndedEvent
        {
            FinalBalance = 5000, FinalEquity = 5200, MaxDrawdownPct = 10.0,
            MaxDailyDrawdownPct = 5.0, TotalTrades = 20
        };
        Assert.Equal(5000, evt.FinalBalance);
        Assert.Equal(20, evt.TotalTrades);
    }

    [Fact]
    public void OptimizationCycleCompletedEvent_Stores_Data()
    {
        var evt = new OptimizationCycleCompletedEvent(2, 0.85, 100);
        Assert.Equal(2, evt.CycleIndex);
        Assert.Equal(0.85, evt.BestFitness);
        Assert.Equal(100, evt.GenerationCount);
    }

    [Fact]
    public void OptimizationGenerationEvent_Defaults()
    {
        var evt = new OptimizationGenerationEvent
        {
            Generation = 5,
            BestFitness = 0.7,
            IsHyperMutation = true
        };
        Assert.Equal(5, evt.Generation);
        Assert.True(evt.IsHyperMutation);
    }

    [Fact]
    public void OrderExecutedEvent_Stores_Trade_Info()
    {
        var evt = new OrderExecutedEvent
        {
            Symbol = "BTCUSDT", OrderType = "Buy", Volume = 0.1, Price = 50000, IsOpen = true
        };
        Assert.Equal("BTCUSDT", evt.Symbol);
        Assert.True(evt.IsOpen);
    }

    [Fact]
    public void WalkForwardWindowCompletedEvent_Stores_Window()
    {
        var start = new DateTime(2025, 1, 1);
        var end = new DateTime(2025, 1, 31);
        var evt = new WalkForwardWindowCompletedEvent(1, start, end, start, end, 0.9);
        Assert.Equal(1, evt.WindowIndex);
        Assert.Equal(start, evt.TrainStart);
    }

    [Fact]
    public void All_Events_Implement_IMessage()
    {
        Assert.IsAssignableFrom<IMessage>(new BacktestStartedEvent());
        Assert.IsAssignableFrom<IMessage>(new BacktestCompletedEvent());
        Assert.IsAssignableFrom<IMessage>(new ConnectionStateEvent(true, ""));
        Assert.IsAssignableFrom<IMessage>(new LiveReconnectEvent(true, 0, ""));
        Assert.IsAssignableFrom<IMessage>(new LiveSessionEndedEvent());
        Assert.IsAssignableFrom<IMessage>(new OptimizationCycleCompletedEvent(0, 0, 0));
        Assert.IsAssignableFrom<IMessage>(new OptimizationGenerationEvent());
        Assert.IsAssignableFrom<IMessage>(new OrderExecutedEvent());
        Assert.IsAssignableFrom<IMessage>(new WalkForwardWindowCompletedEvent(0, default, default, default, default, 0));
    }
}
