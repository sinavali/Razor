namespace Razor.Core.Engine.UnitTests;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Razor.Core.Engine.Extensions;
using Razor.Core.Engine.Kernel;
using Razor.Core.Kernel.Messaging;
using Razor.Core.Kernel.Telemetry;
using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.Adapter;
using Razor.Core.Sdk.Slots.NeuralNetwork;
using Razor.Core.Sdk.Slots.Strategy;

public class KernelServiceLivePauseResumeTests
{
    private readonly ILogger<KernelService> _logger = NullLogger<KernelService>.Instance;
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly IExtensionManager _extensionManager = new StubExtensionManager();
    private readonly IHookRegistry _hookRegistry = new StubHookRegistry();
    private readonly IMessageBus _messageBus = new StubMessageBus();
    private readonly ICoreMetrics _metrics = new StubCoreMetrics();

    [Fact]
    public async Task PauseLiveAsync_UnsubscribesTickHandler()
    {
        var kernel = new KernelService(_extensionManager, _hookRegistry, _messageBus, _metrics, _logger, _loggerFactory);
        var adapter = new StubAdapter();
        var tickHandler = (Action<string, Tick>)((symbol, tick) => { });

        adapter.OnTickReceived += tickHandler;
        var taskState = CreateTaskState(adapter, tickHandler);
        SetActiveTasks(kernel, taskState);

        Assert.Single(adapter.OnTickReceived.GetInvocationList());

        await kernel.PauseLiveAsync("task-1", CancellationToken.None);

        Assert.Null(adapter.OnTickReceived);
    }

    [Fact]
    public async Task ResumeLiveAsync_ResubscribesTickHandler()
    {
        var kernel = new KernelService(_extensionManager, _hookRegistry, _messageBus, _metrics, _logger, _loggerFactory);
        var adapter = new StubAdapter();
        var tickHandler = (Action<string, Tick>)((symbol, tick) => { });

        adapter.OnTickReceived += tickHandler;
        var taskState = CreateTaskState(adapter, tickHandler);
        SetActiveTasks(kernel, taskState);

        adapter.OnTickReceived -= tickHandler;
        Assert.Null(adapter.OnTickReceived);

        await kernel.ResumeLiveAsync("task-1", CancellationToken.None);

        Assert.NotNull(adapter.OnTickReceived);
        Assert.Same(tickHandler, adapter.OnTickReceived);
    }

    [Fact]
    public async Task PauseThenResume_HandlerIsRestored()
    {
        var kernel = new KernelService(_extensionManager, _hookRegistry, _messageBus, _metrics, _logger, _loggerFactory);
        var adapter = new StubAdapter();
        var tickHandler = (Action<string, Tick>)((symbol, tick) => { });

        adapter.OnTickReceived += tickHandler;
        var taskState = CreateTaskState(adapter, tickHandler);
        SetActiveTasks(kernel, taskState);

        await kernel.PauseLiveAsync("task-1", CancellationToken.None);
        Assert.Null(adapter.OnTickReceived);

        await kernel.ResumeLiveAsync("task-1", CancellationToken.None);
        Assert.Same(tickHandler, adapter.OnTickReceived);
    }

    private static object CreateTaskState(IAdapterCapability adapter, Action<string, Tick> tickHandler)
    {
        var cts = new CancellationTokenSource();
        var taskStateType = typeof(KernelService).GetNestedType("TaskState", System.Reflection.BindingFlags.NonPublic);
        return Activator.CreateInstance(
            taskStateType!,
            cts,
            adapter,
            Array.Empty<string>(),
            tickHandler,
            null,
            null,
            null,
            null,
            null,
            null,
            null)!;
    }

    private static void SetActiveTasks(KernelService kernel, object taskState)
    {
        var field = typeof(KernelService).GetField("_activeTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dict = (ConcurrentDictionary<string, object>)field!.GetValue(kernel)!;
        dict["task-1"] = taskState;
    }

    private sealed class StubAdapter : IAdapterCapability
    {
        public string Name => "StubAdapter";
        public IMarketCalculator Calculator => null!;
        public bool IsConnected { get; set; }
        public bool SupportsHistoricalData { get; set; }
        public bool SupportsLiveData { get; set; }
        public bool SupportsExecution { get; set; }
        public Task<bool> ConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(HistoricalDataRequest request, CancellationToken cancellationToken) => Task.FromResult<HistoricalDataResponse>(new HistoricalDataResponse());
        public Task DeleteHistoryFileAsync(string filePath) => Task.CompletedTask;
        public Task NotifyFileSafeToDeleteAsync(string filePath) => Task.CompletedTask;
        public Task SubscribeAsync(string symbol) => Task.CompletedTask;
        public Task UnsubscribeAsync(string symbol) => Task.CompletedTask;
        public event Action<string, Tick>? OnTickReceived;
        public Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest request) => Task.FromResult<AdapterOrderResponse>(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null) => Task.FromResult<AdapterOrderResponse>(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double? volume = null) => Task.FromResult<AdapterOrderResponse>(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> CancelAsync(long ticket) => Task.FromResult<AdapterOrderResponse>(new AdapterOrderResponse());
        public Task<(double Balance, double Equity)> GetAccountInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult((0.0, 0.0));
        public Task<IReadOnlyList<Position>> GetActivePositionsAsync() => Task.FromResult<IReadOnlyList<Position>>(Array.Empty<Position>());
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync() => Task.FromResult<IReadOnlyList<Order>>(Array.Empty<Order>());
        public Task<SymbolProperties?> GetSymbolPropertiesAsync(string symbol, CancellationToken cancellationToken = default) => Task.FromResult<SymbolProperties?>(null);
        public event Action<ExecutionReport>? OnExecutionUpdate;
        public TimeFrame[]? GetSupportedTimeframes(string symbol) => null;
    }

    private sealed class StubExtensionManager : IExtensionManager
    {
        public IAdapterCapability? ActiveAdapter => null;
        public IStrategyCapability? ActiveStrategy => null;
        public INeuralNetworkModel? ActiveNeuralNetwork => null;
        public IHookRegistry HookRegistry => null!;
        public Task DiscoverExtensionsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReloadExtensionsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ActivateExtensionsAsync(string adapterName, string strategyName, string? nnModelName, string[] hookPluginNames, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeployExtensionAsync(string name, byte[] binaryData, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveExtensionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<object> GetManifestAsync(CancellationToken cancellationToken) => Task.FromResult<object>(new object());
        public IStrategyCapability? CreateTransientStrategy(string strategyName) => null;
        public void EnableBehaviorLoggingOnStrategy(string sessionId, int snapshotIntervalSeconds = 10) { }
        public void DisableBehaviorLoggingOnStrategy() { }
        public void RecordSnapshotOnStrategy() { }
    }

    private sealed class StubHookRegistry : IHookRegistry
    {
        public IBacktestHooks Backtest => null!;
        public ILiveHooks Live => null!;
        public IOptimizationHooks Optimization => null!;
        public IReportHooks Report => null!;
        public void ClearAll() { }
        public Task TriggerAsync(string hookName, object context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMessageBus : IMessageBus
    {
        public void Publish<T>(T message) where T : IMessage { }
        public IDisposable Subscribe<T>(Action<T> handler) where T : IMessage => new Unsubscriber(() => { });
        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SubscribeAsync(string topic, Action<string> handler, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnsubscribeAsync(string topic, Action<string> handler, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private sealed class Unsubscriber : IDisposable
        {
            private readonly Action _onDispose;
            public Unsubscriber(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }

    private sealed class StubCoreMetrics : ICoreMetrics
    {
        public bool IsConnected { get; set; }
        public string AdapterName { get; set; } = string.Empty;
        public void SetConnectionState(bool connected, string adapterName) { }
        public void RecordBacktestDuration(string taskId, double seconds) { }
        public void RecordLiveTick(string taskId, string symbol) { }
        public void RecordOrderPlaced(string taskId) { }
        public void RecordOrderFilled(string taskId) { }
        public void IncrementActiveTasks() { }
        public void DecrementActiveTasks() { }
        public void RecordLiveTickLatency(long ticks) { }
        public void RecordGaFitnessImprovement(double improvement) { }
        public void RecordBacktestTicksPerSecond(double ticksPerSec) { }
        public void RecordLiveOrderLatency(long milliseconds) { }
        public void RecordLiveOrderRejection() { }
        public void RecordOptimizationDuration(double seconds) { }
    }
}
