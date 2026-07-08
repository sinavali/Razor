using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.IntegrationTests.Shared;

public class ReportRequest_IntegrationTests
{
    [Fact]
    public void Create_ReportRequest_With_Many_Positions()
    {
        var trades = new List<Position>();
        for (int i = 0; i < 1000; i++)
        {
            trades.Add(new Position
            {
                Ticket = i,
                Symbol = "EURUSD",
                Type = OrderType.Buy,
                Volume = 0.1,
                OpenPrice = 1.0 + i * 0.0001,
                OpenTime = i * 1000,
                ClosePrice = 1.1 + i * 0.0001,
                CloseTime = (i + 1) * 1000,
                Profit = 100 + i * 0.1
            });
        }

        var metrics = new Dictionary<string, double>
        {
            { "Sharpe", 1.5 },
            { "Sortino", 2.1 },
            { "WinRate", 62.5 }
        };

        var report = new ReportRequest
        {
            ReportTitle = "Large Backtest",
            TradeHistory = trades,
            InitialBalance = 100000,
            FinalBalance = 150000,
            MaxDrawdown = 25.0,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            AdditionalMetrics = metrics
        };

        Assert.Equal(1000, report.TradeHistory.Count);
        Assert.Equal(3, report.AdditionalMetrics.Count);
        Assert.Equal(150000, report.FinalBalance);
    }

    [Fact]
    public void Report_With_50000_Positions()
    {
        var trades = new List<Position>();
        for (int i = 0; i < 50000; i++)
        {
            trades.Add(new Position
            {
                Ticket = i,
                Symbol = "EURUSD",
                Type = OrderType.Buy,
                Volume = 0.1,
                OpenPrice = 1.0 + i * 0.00001,
                OpenTime = i * 1000,
                ClosePrice = 1.1 + i * 0.00001,
                CloseTime = (i + 1) * 1000,
                Profit = 10 + i * 0.01
            });
        }

        var report = new ReportRequest
        {
            ReportTitle = "50K Trades",
            TradeHistory = trades,
            InitialBalance = 100000,
            FinalBalance = 200000,
            MaxDrawdown = 30.0,
            StartDate = new DateTime(2020, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            AdditionalMetrics = new Dictionary<string, double>
            {
                { "TotalTrades", 50000 },
                { "WinRate", 55.5 },
                { "ProfitFactor", 1.8 }
            }
        };

        Assert.Equal(50000, report.TradeHistory.Count);
        Assert.Equal(3, report.AdditionalMetrics.Count);
        Assert.Equal(200000, report.FinalBalance);
    }
}
