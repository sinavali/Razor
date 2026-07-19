using Razor.Core.Engine.Hooks;
using Razor.Core.Kernel.Clock;
using Razor.Core.Kernel.Hooks;
using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;

namespace Razor.Core.Engine.UnitTests;

public sealed class AuditTrailLoggingHookPluginTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"audit_trail_test_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SqliteAuditTrailStore_Appends_Immutable_Records()
    {
        using var store = new SqliteAuditTrailStore(_dbPath);

        store.Append(new AuditTrailRecord
        {
            TimestampUtc = "2026-07-18T00:00:00Z",
            TaskId = "live_1",
            OrderId = 42,
            Symbol = "EURUSD",
            Side = "Buy",
            Quantity = 1.5,
            Price = 1.1,
            Status = "executed",
            RejectionReason = string.Empty
        });

        var records = store.ReadAll();

        Assert.Single(records);
        var record = records[0];
        Assert.Equal("live_1", record.TaskId);
        Assert.Equal(42, record.OrderId);
        Assert.Equal("EURUSD", record.Symbol);
        Assert.Equal("Buy", record.Side);
        Assert.Equal(1.5, record.Quantity);
        Assert.Equal(1.1, record.Price);
        Assert.Equal("executed", record.Status);
        Assert.Equal(string.Empty, record.RejectionReason);
    }

    [Fact]
    public void HookPlugin_Writes_Executed_And_Rejected_Records()
    {
        var registry = new HookRegistry();
        var plugin = new AuditTrailLoggingHookPlugin(_dbPath, taskId: "live_1");
        plugin.RegisterHooks(registry);

        var clock = new SystemClock();
        var executedContext = new LiveContext(clock, new Tick(), 1000, 1000, 0, null!, "X", true, "live.order.executed");
        var rejectedContext = new LiveContext(clock, new Tick(), 1000, 1000, 0, null!, "X", true, "live.order.rejected");

        registry.Live.OnOrderExecuted.InvokeActionChain(
            new ExecutionReport
            {
                Ticket = 42,
                Symbol = "EURUSD",
                Type = OrderType.Buy,
                State = ExecutionState.Filled,
                ExecutedVolume = 1.5,
                ExecutedPrice = 1.1,
                Timestamp = clock.GetUtcNow().Ticks
            },
            executedContext);

        registry.Live.OnOrderRejected.InvokeActionChain(
            (new AdapterOrderRequest { Symbol = "GBPUSD", Type = OrderType.Sell, Volume = 2.0, Price = 1.25 }, "Insufficient margin"),
            rejectedContext);

        // Simulate engine reload: registrations are cleared, then the audit hook is re-registered.
        registry.ClearAll();
        plugin.RegisterHooks(registry);

        registry.Live.OnOrderExecuted.InvokeActionChain(
            new ExecutionReport
            {
                Ticket = 99,
                Symbol = "USDJPY",
                Type = OrderType.BuyLimit,
                State = ExecutionState.Filled,
                ExecutedVolume = 0.5,
                ExecutedPrice = 150.0,
                Timestamp = clock.GetUtcNow().Ticks
            },
            executedContext);

        var records = plugin.ReadAllFromStore();
        Assert.Equal(3, records.Count);

        var executed = records[0];
        Assert.Equal("executed", executed.Status);
        Assert.Equal("EURUSD", executed.Symbol);
        Assert.Equal("Buy", executed.Side);
        Assert.Equal(1.5, executed.Quantity);
        Assert.Equal(1.1, executed.Price);

        var rejected = records[1];
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("GBPUSD", rejected.Symbol);
        Assert.Equal("Insufficient margin", rejected.RejectionReason);

        var afterReload = records[2];
        Assert.Equal("executed", afterReload.Status);
        Assert.Equal("USDJPY", afterReload.Symbol);
        Assert.Equal(99, afterReload.OrderId);

        plugin.Dispose();
    }

    [Fact]
    public void HookPlugin_Does_Not_Throw_On_Executed_And_Rejected()
    {
        var registry = new HookRegistry();
        var plugin = new AuditTrailLoggingHookPlugin(_dbPath, taskId: "live_1");
        plugin.RegisterHooks(registry);

        var clock = new SystemClock();
        var context = new LiveContext(clock, new Tick(), 1000, 1000, 0, null!, "X", true, "live.order.executed");

        var exception = Record.Exception(() =>
        {
            registry.Live.OnOrderExecuted.InvokeActionChain(
                new ExecutionReport { Ticket = 1, Symbol = "EURUSD", Type = OrderType.Buy, State = ExecutionState.Filled, ExecutedVolume = 1, ExecutedPrice = 1.1, Timestamp = clock.GetUtcNow().Ticks },
                context);
            registry.Live.OnOrderRejected.InvokeActionChain(
                (new AdapterOrderRequest { Symbol = "EURUSD", Type = OrderType.Sell }, "reason"),
                context);
        });

        Assert.Null(exception);
        plugin.Dispose();
    }
}
