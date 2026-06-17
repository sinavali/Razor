using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class ReportRequestTests
{
    [Fact]
    public void Default_Values_Are_Set()
    {
        var report = new ReportRequest
        {
            ReportTitle = "Backtest Report",
            TradeHistory = new List<Position>(),
            InitialBalance = 10000,
            FinalBalance = 12000,
            MaxDrawdown = 15.5,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31)
        };

        Assert.Equal("Backtest Report", report.ReportTitle);
        Assert.Empty(report.TradeHistory);
        Assert.Equal(10000, report.InitialBalance);
        Assert.Equal(12000, report.FinalBalance);
        Assert.Equal(15.5, report.MaxDrawdown);
        Assert.Equal(new DateTime(2024, 1, 1), report.StartDate);
        Assert.Equal(new DateTime(2024, 12, 31), report.EndDate);
        Assert.NotNull(report.AdditionalMetrics);
        Assert.Empty(report.AdditionalMetrics);
    }

    [Fact]
    public void AdditionalMetrics_Can_Be_Populated()
    {
        var metrics = new Dictionary<string, double>
        {
            { "SharpeRatio", 1.5 },
            { "SortinoRatio", 2.1 },
            { "ProfitFactor", 1.8 }
        };

        var report = new ReportRequest
        {
            ReportTitle = "Test",
            TradeHistory = new List<Position>(),
            InitialBalance = 1000,
            FinalBalance = 2000,
            MaxDrawdown = 10,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow,
            AdditionalMetrics = metrics
        };

        Assert.Equal(3, report.AdditionalMetrics.Count);
        Assert.Equal(1.5, report.AdditionalMetrics["SharpeRatio"]);
    }

    [Fact]
    public void With_Changes_Preserves_Immutability()
    {
        var original = new ReportRequest
        {
            ReportTitle = "Original",
            TradeHistory = new List<Position>(),
            InitialBalance = 1000,
            FinalBalance = 2000,
            MaxDrawdown = 5,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow
        };

        var modified = original with { ReportTitle = "Modified", MaxDrawdown = 10 };

        Assert.Equal("Original", original.ReportTitle);
        Assert.Equal("Modified", modified.ReportTitle);
        Assert.Equal(10, modified.MaxDrawdown);
    }

    [Fact]
    public void Equality_Same_Values()
    {
        var a = new ReportRequest
        {
            ReportTitle = "R1",
            TradeHistory = new List<Position>(),
            InitialBalance = 5000,
            FinalBalance = 6000,
            MaxDrawdown = 20,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 6, 30)
        };

        var b = a with { };

        Assert.Equal(a, b);
    }

    [Fact]
    public void Not_Equal_When_Different()
    {
        var a = new ReportRequest
        {
            ReportTitle = "R1",
            TradeHistory = new List<Position>(),
            InitialBalance = 5000,
            FinalBalance = 6000,
            MaxDrawdown = 20,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 6, 30)
        };

        var b = a with { ReportTitle = "R2" };

        Assert.NotEqual(a, b);
    }
}
