using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots;
using System.Collections.Immutable;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class StrategyBaseTests
{
    private sealed class TestableStrategy : StrategyBase
    {
        public new string PrimarySymbol => base.PrimarySymbol;
        public new StrategySpecification Spec => base.Spec;
        public new IIndicatorRegistry Indicators => base.Indicators;
        public new IBroker Broker => base.Broker;
        public new TickWindow TickWindow => base.TickWindow;
        public new bool RequiresNeuralNetwork => base.RequiresNeuralNetwork;
        public new int TotalGeneCount => base.TotalGeneCount;
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
        public new Task<AdapterOrderResponse> CancelOrderAsync(long ticket) => base.CancelOrderAsync(ticket);
        public new Task CloseAllAsync(OrderType? type = null) => base.CloseAllAsync(type);
        public new Task CloseAllAsync(string symbol, OrderType? type = null) => base.CloseAllAsync(symbol, type);
    }

    private sealed class SymbolRecordingStrategy : StrategyBase
    {
        public string? LastSymbol
        {
            get; private set;
        }
        public override void OnTick(string symbol, Tick tick) => LastSymbol = symbol;
    }

    private sealed class GeneExportStrategy : StrategyBase
    {
        [Gene(0, 100, 1, GeneType.Discrete)]
        public int Fast { get; set; } = 50;

        [Gene(0.1, 5.0, 1.0, GeneType.Discrete, Order = 1)]
        public double Risk { get; set; } = 2.0;
    }

    private sealed class DummyRegistry : IIndicatorRegistry
    {
        public IReadOnlyList<Indicator> ActiveIndicators => Array.Empty<Indicator>();
        public T Get<T>(params object[] args) where T : Indicator => null!;
        public bool Unregister(Indicator indicator) => false;
        public void DisposeAll()
        {
        }
    }

    private sealed class CountingBroker : IBroker
    {
        public int MarketOrderCalls;
        public int ModifyOrderCalls;
        public int CancelOrderCalls;
        public int CloseAllCalls;

        public double Balance => 0; public double Equity => 0; public double MarginUsed => 0;
        public double FreeMargin => 0; public double MaxDrawdown => 0; public double MaxDailyDrawdown => 0;
        public bool IsWarmup => false;
        public Task InitializeLiveStateAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SyncStateAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<AdapterOrderResponse> ExecuteMarketOrderAsync(string s, OrderType t, double v, double sl, double tp, string c)
        {
            MarketOrderCalls++;
            return Task.FromResult(new AdapterOrderResponse());
        }

        public Task<AdapterOrderResponse> PlacePendingOrderAsync(string s, OrderType t, double v, double p, double sl, double tp, string c)
            => Task.FromResult(new AdapterOrderResponse());

        public Task<AdapterOrderResponse> ModifyOrderAsync(long t, double? sl, double? tp, double? p)
        {
            ModifyOrderCalls++;
            return Task.FromResult(new AdapterOrderResponse());
        }

        public Task<AdapterOrderResponse> CancelOrderAsync(long t)
        {
            CancelOrderCalls++;
            return Task.FromResult(new AdapterOrderResponse());
        }

        public Task<AdapterOrderResponse> ClosePositionAsync(long t, double v) => Task.FromResult(new AdapterOrderResponse());

        public Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(string s, OrderType? t)
        {
            CloseAllCalls++;
            return Task.FromResult((IReadOnlyList<AdapterOrderResponse>)Array.Empty<AdapterOrderResponse>());
        }

        public Task<bool> HasOpenPositionAsync(string s, OrderType? t, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? s, CancellationToken ct) => Task.FromResult((IReadOnlyList<Position>)Array.Empty<Position>());
        public Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<Position>)Array.Empty<Position>());
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<Order>)Array.Empty<Order>());
    }

    private sealed class TestNetwork : INeuralNetworkModel
    {
        private double[] _params = new double[3];
        public string ModelType => "Test";
        public int InputSize => 2;
        public int OutputSize => 1;
        public int ParameterCount => 3;
        public double[] Predict(double[] inputs) => [0];
        public void LoadParameters(double[] genes) => _params = (double[])genes.Clone();
        public double[] ExportParameters() => (double[])_params.Clone();
        public void Reset()
        {
        }
        public byte[] SerializeState() => [];
        public void DeserializeState(byte[] state)
        {
        }
    }

    private static readonly ImmutableArray<SymbolRequest> BtcSymbols =
        ImmutableArray.Create(new SymbolRequest("BTCUSDT", ImmutableArray.Create(TimeFrame.M1)));
    private static readonly string[] SymbolsEurUsd = { "EURUSD" };
    private static readonly TimeFrame[] TimeframeM1 = { TimeFrame.M1 };
    private static readonly double[] ExpectedGeneExport = { 50.0, 2.0 };
    private static readonly double[] expected = new[] { 10.0, 1.5 };

    // ── Lifecycle ─────────────────────────────────────────────────

    [Fact]
    public async Task OnTick_Receives_Symbol()
    {
        using var strategy = new SymbolRecordingStrategy();
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        await strategy.OnStartAsync(new DummyRegistry());
        strategy.OnTick("BTCUSDT", new Tick(0, 0, 0, 0));
        Assert.Equal("BTCUSDT", strategy.LastSymbol);
    }

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
        var spec = StrategySpecification.CreateValidated(5000, 50, BtcSymbols);
        await strategy.OnConfigureAsync(spec);
        Assert.Equal(spec, strategy.Spec);
        Assert.Equal(5000, strategy.Spec.InitialBalance);
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
    public async Task OnStopAsync_Does_Not_Throw()
    {
        using var strategy = new TestableStrategy();
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        await strategy.OnStartAsync(new DummyRegistry());
        await strategy.OnStopAsync();
    }

    [Fact]
    public async Task OnTick_Default_Does_Nothing()
    {
        using var strategy = new TestableStrategy();
        var spec = StrategySpecification.CreateValidated(1000, 10, BtcSymbols);
        await strategy.OnConfigureAsync(spec);
        await strategy.OnStartAsync(new DummyRegistry());

        strategy.OnTick("BTCUSDT", new Tick(0, 0, 0, 0));

        // Default OnTick does nothing; verify configuration and indicators remain intact.
        Assert.Equal("BTCUSDT", strategy.PrimarySymbol);
        Assert.Equal(1000, strategy.Spec.InitialBalance);
        Assert.NotNull(strategy.Indicators);
        Assert.Equal(0, strategy.TotalGeneCount);
    }

    // ── Gene Support ─────────────────────────────────────────────

    [Fact]
    public void InjectGenes_Acquires_Lock()
    {
        using var strategy = new TestableStrategy();
        strategy.InjectGenes([1.0]);
        Assert.False(strategy.GeneLock.IsWriteLockHeld);
    }

    [Fact]
    public async Task ExportGenes_Without_NeuralNetwork_Returns_PropertyGenes()
    {
        using var strategy = new GeneExportStrategy { Fast = 50, Risk = 2.0 };
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        var genes = strategy.ExportGenes();
        Assert.Equal(ExpectedGeneExport, genes);
    }

    [Fact]
    public async Task ExportGenes_With_NeuralNetwork_Appends_Network_Parameters()
    {
        using var strategy = new GeneExportStrategy { Fast = 50, Risk = 2.0 };
        var nn = new TestNetwork();
        strategy.NeuralNetwork = nn;
        Assert.NotNull(strategy.NeuralNetwork);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        Assert.NotNull(strategy.NeuralNetwork);
        var genes = strategy.ExportGenes();
        Assert.Equal(5, genes.Length);
    }

    [Fact]
    public void TotalGeneCount_Without_NeuralNetwork_Counts_Property_Genes()
    {
        using var strategy = new GeneExportStrategy();
        strategy.NeuralNetwork = null;
        Assert.Equal(2, strategy.TotalGeneCount);
    }

    [Fact]
    public void TotalGeneCount_With_NeuralNetwork_Includes_Network_Count()
    {
        using var strategy = new GeneExportStrategy();
        strategy.NeuralNetwork = new TestNetwork();
        Assert.Equal(5, strategy.TotalGeneCount);
    }

    [Fact]
    public void TotalGeneCount_Default_Is_Zero()
    {
        using var strategy = new TestableStrategy();
        Assert.Equal(0, strategy.TotalGeneCount);
    }

    // ── Neural Network ──────────────────────────────────────────

    [Fact]
    public void RequiresNeuralNetwork_Default_Is_False()
    {
        using var strategy = new TestableStrategy();
        Assert.False(strategy.RequiresNeuralNetwork);
    }

    // ── Order Helpers ────────────────────────────────────────────

    [Fact]
    public async Task BuyAsync_Symbol_Overload_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.BuyAsync("EURUSD", 0.1);
        Assert.Equal(1, broker.MarketOrderCalls);
    }

    [Fact]
    public async Task BuyAsync_PrimarySymbol_Overload_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.BuyAsync(0.2);
        Assert.Equal(1, broker.MarketOrderCalls);
    }

    [Fact]
    public async Task SellAsync_Symbol_Overload_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.SellAsync("EURUSD", 0.1);
        Assert.Equal(1, broker.MarketOrderCalls);
    }

    [Fact]
    public async Task SellAsync_PrimarySymbol_Overload_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.SellAsync(0.2);
        Assert.Equal(1, broker.MarketOrderCalls);
    }

    [Fact]
    public async Task ModifyOrderAsync_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.ModifyOrderAsync(123, 1.0, 2.0, 1.5);
        Assert.Equal(1, broker.ModifyOrderCalls);
    }

    [Fact]
    public async Task CancelOrderAsync_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.CancelOrderAsync(123);
        Assert.Equal(1, broker.CancelOrderCalls);
    }

    [Fact]
    public async Task CloseAllAsync_PrimarySymbol_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.CloseAllAsync();
        Assert.Equal(1, broker.CloseAllCalls);
    }

    [Fact]
    public async Task CloseAllAsync_Symbol_Overload_Calls_Broker()
    {
        using var strategy = new TestableStrategy();
        var broker = new CountingBroker();
        using var tickWindow = new TickWindow(SymbolsEurUsd, TimeframeM1);
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        strategy.WireUp(broker, tickWindow);
        await strategy.CloseAllAsync("EURUSD");
        Assert.Equal(1, broker.CloseAllCalls);
    }

    // ── Disposal ─────────────────────────────────────────────────

    [Fact]
    public void Dispose_Releases_Lock()
    {
        var strategy = new TestableStrategy();
        strategy.Dispose();
        Assert.Throws<ObjectDisposedException>(() => strategy.GeneLock.EnterReadLock());
    }

    // ── Additional coverage ─────────────────────────────────────

    private sealed class WindowCompletingStrategy : StrategyBase
    {
        public int WindowCompletedCount;
        protected override Task OnWindowCompletedAsync(string symbol, TimeFrame tf)
        {
            WindowCompletedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NullNetwork : INeuralNetworkModel
    {
        public string ModelType => "Null";
        public int InputSize => 1;
        public int OutputSize => 1;
        public int ParameterCount => 0;
        public double[] Predict(double[] inputs) => [0];
        public void LoadParameters(double[] genes)
        {
        }
        public double[] ExportParameters() => [];
        public void Reset()
        {
        }
        public byte[] SerializeState() => [];
        public void DeserializeState(byte[] state)
        {
        }
    }

    [Fact]
    public async Task NotifyWindowCompletedAsync_Invokes_Protected_Method()
    {
        using var strategy = new WindowCompletingStrategy();
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        await strategy.NotifyWindowCompletedAsync("BTCUSDT", TimeFrame.M1);
        Assert.Equal(1, strategy.WindowCompletedCount);
    }

    [Fact]
    public async Task ExportGenes_With_NullParameterCount_Network()
    {
        using var strategy = new GeneExportStrategy { Fast = 10, Risk = 1.5 };
        var nn = new NullNetwork();
        strategy.NeuralNetwork = nn;
        await strategy.OnConfigureAsync(StrategySpecification.CreateValidated(1000, 10, BtcSymbols));
        var genes = strategy.ExportGenes();
        Assert.Equal(expected, genes);
    }
}
