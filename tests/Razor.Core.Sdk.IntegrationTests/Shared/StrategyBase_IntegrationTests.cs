using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots;
using Razor.Core.Sdk.Slots.NeuralNetwork;
using System.Collections.Immutable;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class StrategyBase_IntegrationTests
{
    private sealed class TestStrategy : StrategyBase
    {
        public int TickCount;
        public new string PrimarySymbol => base.PrimarySymbol;
        public override void OnTick(string symbol, Tick tick) => TickCount++;
    }

    private sealed class CountingStrategy : StrategyBase
    {
        public int Ticks;
        public override void OnTick(string symbol, Tick tick) => Ticks++;
    }

    private sealed class GeneStrategy : StrategyBase
    {
        [Gene(0, 100, 1, GeneType.Discrete)]
        public int Period { get; set; } = 50;

        [Gene(0.5, 5.0, 0, GeneType.Continuous)]
        public double Factor { get; set; } = 2.0;
    }

    private sealed class WindowTrackingStrategy : StrategyBase
    {
        public int CompletedCount;
        protected override Task OnWindowCompletedAsync(string symbol, TimeFrame tf)
        {
            CompletedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NeuralNetwork : INeuralNetworkModel
    {
        private double[] _params = new double[5];
        public string ModelType => "TestNN";
        public int InputSize => 3;
        public int OutputSize => 2;
        public int ParameterCount => 5;
        public double[] Predict(double[] inputs) => [0, 0];
        public void LoadParameters(double[] genes) => Array.Copy(genes, _params, 5);
        public double[] ExportParameters() => (double[])_params.Clone();
        public void Reset()
        {
        }
        public byte[] SerializeState() => [];
        public void DeserializeState(byte[] state)
        {
        }
    }

    private sealed class DummyBroker : IBroker
    {
        public double Balance => 10000;
        public double Equity => 10000;
        public double MarginUsed => 0;
        public double FreeMargin => 10000;
        public double MaxDrawdown => 0;
        public double MaxDailyDrawdown => 0;
        public bool IsWarmup => false;
        public Task InitializeLiveStateAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SyncStateAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<AdapterOrderResponse> ExecuteMarketOrderAsync(string s, OrderType t, double v, double sl, double tp, string c) =>
            Task.FromResult(new AdapterOrderResponse { Success = true, Ticket = 1 });
        public Task<AdapterOrderResponse> PlacePendingOrderAsync(string s, OrderType t, double v, double p, double sl, double tp, string c) =>
            Task.FromResult(new AdapterOrderResponse { Success = true, Ticket = 2 });
        public Task<AdapterOrderResponse> ModifyOrderAsync(long t, double? sl, double? tp, double? p) =>
            Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<AdapterOrderResponse> CancelOrderAsync(long t) =>
            Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<AdapterOrderResponse> ClosePositionAsync(long t, double v) =>
            Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(string s, OrderType? t) =>
            Task.FromResult<IReadOnlyList<AdapterOrderResponse>>(Array.Empty<AdapterOrderResponse>());
        public Task<bool> HasOpenPositionAsync(string s, OrderType? t, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? s, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Position>>(Array.Empty<Position>());
        public Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Position>>(Array.Empty<Position>());
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Order>>(Array.Empty<Order>());
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

    private static readonly string[] BtcSymbols = { "BTCUSDT" };
    private static readonly ImmutableArray<SymbolRequest> SymbolsEurUsd =
        ImmutableArray.Create(new SymbolRequest("EURUSD", ImmutableArray.Create(TimeFrame.M1)));
    private static readonly ImmutableArray<SymbolRequest> BtcSymbolRequests =
        ImmutableArray.Create(new SymbolRequest("BTCUSDT", ImmutableArray.Create(TimeFrame.M1)));
    private static readonly string[] symbolsArray = new[] { "EURUSD" };
    private static readonly double[] genes = new double[] { 1.0 };

    [Fact]
    public async Task Lifecycle_With_Broker_And_TickWindow()
    {
        using var strategy = new TestStrategy();
        var spec = StrategySpecification.CreateValidated(10000, 50, BtcSymbolRequests);

        await strategy.OnConfigureAsync(spec);
        await strategy.OnStartAsync(new DummyRegistry());

        var broker = new DummyBroker();
        using var tickWindow = new TickWindow(BtcSymbols, new[] { TimeFrame.M1 });
        strategy.WireUp(broker, tickWindow);

        strategy.OnTick("BTCUSDT", new Tick(0, 0, 0, 0));
        strategy.OnTick("BTCUSDT", new Tick(1, 0, 0, 0));
        Assert.Equal(2, strategy.TickCount);

        await strategy.OnStopAsync();
    }

    [Fact]
    public async Task PrimarySymbol_Matches_Configuration()
    {
        using var strategy = new TestStrategy();
        var spec = StrategySpecification.CreateValidated(1000, 10,
            ImmutableArray.Create(new SymbolRequest("ETHUSD", ImmutableArray.Create(TimeFrame.H1))));
        await strategy.OnConfigureAsync(spec);
        Assert.Equal("ETHUSD", strategy.PrimarySymbol);
    }

    [Fact]
    public async Task Process_100K_Ticks_Through_Strategy()
    {
        using var strategy = new CountingStrategy();
        var spec = StrategySpecification.CreateValidated(10000, 50, BtcSymbolRequests);
        await strategy.OnConfigureAsync(spec);

        using var tickWindow = new TickWindow(BtcSymbols, [TimeFrame.M1]);
        strategy.WireUp(new DummyBroker(), tickWindow);
        for (int i = 0; i < 100_000; i++)
        {
            strategy.OnTick("BTCUSDT", new Tick(i, 0, 0, 0));
        }

        Assert.Equal(100_000, strategy.Ticks);
    }

    [Fact]
    public async Task Gene_Injection_And_Export_With_Neural_Network()
    {
        using var strategy = new GeneStrategy();
        var nn = new NeuralNetwork();
        strategy.NeuralNetwork = nn;

        var spec = StrategySpecification.CreateValidated(5000, 20, SymbolsEurUsd);
        await strategy.OnConfigureAsync(spec);

        var genes = GeneInjector.ExtractAndInitializeGenes(strategy, nn, 42);
        Assert.Equal(7, genes.Length);
        Assert.Equal(7, strategy.TotalGeneCount);

        strategy.InjectGenes(genes);
        var exported = strategy.ExportGenes();
        Assert.Equal(7, exported.Length);
        Assert.Equal(genes.Take(2), exported.Take(2));
    }

    [Fact]
    public async Task Full_Lifecycle_With_Broker_And_Window()
    {
        using var strategy = new WindowTrackingStrategy();
        var spec = StrategySpecification.CreateValidated(10000, 50, SymbolsEurUsd);
        await strategy.OnConfigureAsync(spec);
        await strategy.OnStartAsync(new DummyRegistry());

        using var window = new TickWindow(symbolsArray, new[] { TimeFrame.M1 });
        var broker = new DummyBroker();
        strategy.WireUp(broker, window);

        window.PushTick("EURUSD", new Tick(0, 1.0, 1.1, 1));
        window.PushTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 1.5, 1.6, 2));

        strategy.OnTick("EURUSD", new Tick(0, 1.0, 1.1, 1));
        strategy.OnTick("EURUSD", new Tick(TimeSpan.TicksPerMinute, 1.5, 1.6, 2));

        await strategy.OnStopAsync();
    }

    [Fact]
    public async Task ExportGenes_Without_Network_Returns_PropertyGenes_Only()
    {
        using var strategy = new GeneStrategy();
        strategy.NeuralNetwork = null;
        var spec = StrategySpecification.CreateValidated(5000, 20, SymbolsEurUsd);
        await strategy.OnConfigureAsync(spec);

        var genes = strategy.ExportGenes();
        Assert.Equal(2, genes.Length);
        // Alphabetical order: Factor (2.0), Period (50)
        Assert.Equal(2.0, genes[0]);
        Assert.Equal(50.0, genes[1]);
    }

    [Fact]
    public void InjectGenes_With_Fewer_Genes_Than_Properties_Throws()
    {
        using var strategy = new GeneStrategy();
        Assert.Throws<ArgumentException>(() => strategy.InjectGenes(genes));
    }

    [Fact]
    public async Task OnWindowCompletedAsync_Default_Does_Nothing()
    {
        using var strategy = new CountingStrategy();
        var spec = StrategySpecification.CreateValidated(1000, 10, BtcSymbolRequests);
        await strategy.OnConfigureAsync(spec);
        await strategy.NotifyWindowCompletedAsync("BTCUSDT", TimeFrame.M1);
        // No exception means success
    }

    [Fact]
    public void TotalGeneCount_Reflects_PropertyGenes()
    {
        using var strategy = new GeneStrategy();
        Assert.Equal(2, strategy.TotalGeneCount);
    }

    [Fact]
    public void RequiresNeuralNetwork_Default_Is_False()
    {
        using var strategy = new CountingStrategy();
        Assert.False(strategy.RequiresNeuralNetwork);
    }

    [Fact]
    public void ExportGenes_With_Null_Network_Returns_Only_PropertyGenes()
    {
        using var strategy = new GeneStrategy();
        strategy.NeuralNetwork = null;
        var genes = strategy.ExportGenes();
        Assert.Equal(2, genes.Length);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Twice()
    {
        var strategy = new GeneStrategy();
        strategy.Dispose();
        strategy.Dispose();
    }
}
