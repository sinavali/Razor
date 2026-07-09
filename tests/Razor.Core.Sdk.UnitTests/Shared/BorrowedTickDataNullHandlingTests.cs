using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots;

namespace Razor.Core.Sdk.UnitTests.Shared;

public sealed class BorrowedTickDataNullHandlingTests
{
    private sealed class FakeAdapter : IAdapterCapability
    {
        public string Name => "Fake";
        public IMarketCalculator Calculator => null!;
        public bool IsConnected => false;
        public bool SupportsHistoricalData => true;
        public bool SupportsLiveData => false;
        public bool SupportsExecution => false;
        public TimeFrame[]? GetSupportedTimeframes(string symbol) => null;
        public Task<bool> ConnectAsync(CancellationToken ct) => Task.FromResult(false);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(HistoricalDataRequest r, CancellationToken ct) => Task.FromResult(new HistoricalDataResponse());
        public Task DeleteHistoryFileAsync(string path) => Task.CompletedTask;
        public Task NotifyFileSafeToDeleteAsync(string path) => Task.CompletedTask;
        public Task SubscribeAsync(string symbol) => Task.CompletedTask;
        public Task UnsubscribeAsync(string symbol) => Task.CompletedTask;
#pragma warning disable CS0067
        public event Action<string, Tick>? OnTickReceived;
        public event Action<ExecutionReport>? OnExecutionUpdate;
#pragma warning restore CS0067
        public Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest r) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ModifyOrderAsync(long t, double? sl, double? tp, double? price) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ClosePositionAsync(long t, double? v) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> CancelAsync(long t) => Task.FromResult(new AdapterOrderResponse());
        public Task<(double, double)> GetAccountInfoAsync(CancellationToken ct) => Task.FromResult((0.0, 0.0));
        public Task<IReadOnlyList<Position>> GetActivePositionsAsync() => Task.FromResult<IReadOnlyList<Position>>([]);
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync() => Task.FromResult<IReadOnlyList<Order>>([]);
        public Task<SymbolProperties?> GetSymbolPropertiesAsync(string s, CancellationToken ct) => Task.FromResult<SymbolProperties?>(null);
    }

    [Fact]
    public void Constructor_Null_MappedLists_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BorrowedTickData(
            Array.Empty<IReadOnlyList<Tick>>(),
            Array.Empty<string>(),
            null!,
            Array.Empty<string>(),
            new FakeAdapter()));
    }

    [Fact]
    public void Constructor_Null_FilePaths_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BorrowedTickData(
            Array.Empty<IReadOnlyList<Tick>>(),
            Array.Empty<string>(),
            Array.Empty<MemoryMappedTickList>(),
            null!,
            new FakeAdapter()));
    }

    [Fact]
    public void Constructor_Null_Adapter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BorrowedTickData(
            Array.Empty<IReadOnlyList<Tick>>(),
            Array.Empty<string>(),
            Array.Empty<MemoryMappedTickList>(),
            Array.Empty<string>(),
            null!));
    }

    private static readonly string[] symbols = new[] { "A" };
    private static readonly string[] symbolsArray = new[] { "A" };

    [Fact]
    public void Constructor_MappedLists_Count_Mismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BorrowedTickData(
            new IReadOnlyList<Tick>[] { Array.Empty<Tick>() },
            symbols,
            Array.Empty<MemoryMappedTickList>(),
            Array.Empty<string>(),
            new FakeAdapter()));
    }

    [Fact]
    public void Constructor_FilePaths_Count_Mismatch_Throws()
    {
        var mmList = new MemoryMappedTickList(Path.GetTempFileName());
        try
        {
            Assert.Throws<ArgumentException>(() => new BorrowedTickData(
                new IReadOnlyList<Tick>[] { Array.Empty<Tick>() },
                symbolsArray,
                new[] { mmList },
                Array.Empty<string>(),
                new FakeAdapter()));
        }
        finally
        {
            mmList.Dispose();
            File.Delete(Path.GetTempFileName() + ".chrs");
        }
    }
}
