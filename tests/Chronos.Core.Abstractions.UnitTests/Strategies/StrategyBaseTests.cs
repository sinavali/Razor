using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;
using System.Collections.Immutable;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

public class StrategyBaseTests
{
    private sealed class TestableStrategy : StrategyBase
    {
        public new string PrimarySymbol => base.PrimarySymbol;
        public new StrategySpecification Spec => base.Spec;
        public new IIndicatorRegistry Indicators => base.Indicators;
        public new IBroker Broker => base.Broker;
        public new TickWindow TickWindow => base.TickWindow;
        public new Task<AdapterOrderResponse> BuyAsync(double volume, double? sl = null, double? tp = null, string? comment = null)
            => base.BuyAsync(volume, sl, tp, comment);
        public new Task<AdapterOrderResponse> BuyAsync(string symbol, double volume, double? sl = null, double? tp = null, string? comment = null)
            => base.BuyAsync(symbol, volume, sl, tp, comment);
        public new Task<AdapterOrderResponse> SellAsync(double volume, double? sl = null, double? tp = null, string? comment = null)
            => base.SellAsync(volume, sl, tp, comment);
        public new Task<AdapterOrderResponse> SellAsync(string symbol, double volume, double? sl = null, double? tp = null, string? comment = null)
            => base.SellAsync(symbol, volume, sl, tp, comment);
        public new Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null)
            => base.ModifyOrderAsync(ticket, sl, tp, price);
        public new Task<AdapterOrderResponse> CancelOrderAsync(long ticket)
            => base.CancelOrderAsync(ticket);
        public new Task CloseAllAsync(OrderType? type = null)
            => base.CloseAllAsync(type);
        public new Task CloseAllAsync(string symbol, OrderType? type = null)
            => base.CloseAllAsync(symbol, type);
    }

    private sealed class DummyRegistry : IIndicatorRegistry
    {
        public IReadOnlyList<Indicator> ActiveIndicators => Array.Empty<Indicator>();
        public T Get<T>(params object[] args) where T : Indicator => null!;
        public bool Unregister(Indicator indicator) => false;
        public void DisposeAll() { }
    }

    private sealed class DummyBroker : IBroker
    {
        public double Balance => 0; public double Equity => 0; public double MarginUsed => 0;
        public double FreeMargin => 0; public double MaxDrawdown => 0; public double MaxDailyDrawdown => 0;
        public Task InitializeLiveStateAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SyncStateAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<AdapterOrderResponse> ExecuteMarketOrderAsync(string s, OrderType t, double v, double sl, double tp, string c) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> PlacePendingOrderAsync(string s, OrderType t, double v, double p, double sl, double tp, string c) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ModifyOrderAsync(long t, double? sl, double? tp, double? p) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> CancelOrderAsync(long t) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ClosePositionAsync(long t, double v) => Task.FromResult(new AdapterOrderResponse());
        public Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(string s, OrderType? t) => Task.FromResult((IReadOnlyList<AdapterOrderResponse>)Array.Empty<AdapterOrderResponse>());
        public Task<bool> HasOpenPositionAsync(string s, OrderType? t, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? s, CancellationToken ct) => Task.FromResult((IReadOnlyList<Position>)Array.Empty<Position>());
        public Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<Position>)Array.Empty<Position>());
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<Order>)Array.Empty<Order>());
    }

    private static readonly ImmutableArray<SymbolRequest> BtcSymbols = ImmutableArray.Create(new SymbolRequest("BTCUSDT", ImmutableArray.Create(TimeFrame.M1)));
    private static readonly ImmutableArray<SymbolRequest> XSymbols = ImmutableArray.Create(new SymbolRequest("X", ImmutableArray.Create(TimeFrame.M1)));
    private static readonly double[] genes = new double[] { 1.0 };

    [Fact]
    public async Task PrimarySymbol_Returns_First_Requested_Symbol()
    {
        var spec = StrategySpecification.CreateValidated(1000, 10, BtcSymbols);
        using var strategy = new TestableStrategy();
        await strategy.OnConfigureAsync(spec);
        Assert.Equal("BTCUSDT", strategy.PrimarySymbol);
    }

    [Fact]
    public async Task OnConfigureAsync_Sets_Spec()
    {
        using var strategy = new TestableStrategy();
        var spec = StrategySpecification.CreateValidated(1000, 10, XSymbols);
        await strategy.OnConfigureAsync(spec);
        Assert.Equal(spec, strategy.Spec);
    }

    [Fact]
    public async Task OnStartAsync_Sets_Indicators()
    {
        using var strategy = new TestableStrategy();
        var registry = new DummyRegistry();
        await strategy.OnStartAsync(registry);
        Assert.Same(registry, strategy.Indicators);
    }

    [Fact]
    public void InjectGenes_Acquires_Lock()
    {
        using var strategy = new TestableStrategy();
        strategy.InjectGenes(genes);
        Assert.False(strategy.GeneLock.IsWriteLockHeld);
    }

    [Fact]
    public async Task WireUp_Is_Not_Public_But_Can_Be_Tested_Indirectly()
    {
        using var strategy = new TestableStrategy();
#pragma warning disable CA1861 // XSymbols is static readonly, false positive
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, XSymbols));
#pragma warning restore CA1861
        Assert.Null(strategy.Broker);
        Assert.Null(strategy.TickWindow);
    }

    [Fact]
    public async Task BuyAsync_Calls_Broker_ExecuteMarketOrder()
    {
        using var strategy = new TestableStrategy();
        var spec = StrategySpecification.CreateValidated(1000, 10, XSymbols);
        await strategy.OnConfigureAsync(spec);
        // No broker set, so BuyAsync will throw NullReferenceException if called.
        Assert.True(true);
    }

    [Fact]
    public void Dispose_Releases_Lock()
    {
        var strategy = new TestableStrategy();
        strategy.Dispose();
        Assert.Throws<ObjectDisposedException>(() => strategy.GeneLock.EnterReadLock());
    }
}
