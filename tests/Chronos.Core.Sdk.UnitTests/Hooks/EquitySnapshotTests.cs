using Chronos.Core.Sdk.Hooks;

namespace Chronos.Core.Sdk.UnitTests.Hooks;

public class EquitySnapshotTests
{
    [Fact]
    public void Constructor_Stores_Values()
    {
        var snapshot = new EquitySnapshot(1000, 900, 10.5, 5.0);
        Assert.Equal(1000, snapshot.Equity);
        Assert.Equal(900, snapshot.Balance);
        Assert.Equal(10.5, snapshot.Drawdown);
        Assert.Equal(5.0, snapshot.DailyDrawdown);
    }

    [Fact]
    public void With_Zero_Drawdown()
    {
        var snapshot = new EquitySnapshot(500, 500, 0, 0);
        Assert.Equal(0, snapshot.Drawdown);
        Assert.Equal(0, snapshot.DailyDrawdown);
    }
}
