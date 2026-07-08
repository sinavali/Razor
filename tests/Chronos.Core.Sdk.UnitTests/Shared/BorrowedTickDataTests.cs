#pragma warning disable CA2007
using Chronos.Core.Sdk.Shared;
using Chronos.Core.Sdk.Slots;

namespace Chronos.Core.Sdk.UnitTests.Shared;

public sealed class BorrowedTickDataTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }

        GC.SuppressFinalize(this);
    }

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

        public Task NotifyFileSafeToDeleteAsync(string path)
        {
            NotifiedPaths.Add(path);
            return Task.CompletedTask;
        }

        public List<string> NotifiedPaths { get; } = [];
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

    private static readonly IReadOnlyList<Tick>[] EmptyStreams = [];
    private static readonly string[] EmptyStrings = [];
    private static readonly MemoryMappedTickList[] EmptyMmList = [];
    private static readonly Tick[] TestTicks = [new(1, 2, 3, 4)];
    private static readonly string[] SymbolsX = ["X"];
    private static readonly string[] SymbolsBtc = ["BTCUSDT"];

    [Fact]
    public void Constructor_Null_Streams_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BorrowedTickData(null!, EmptyStrings, EmptyMmList, EmptyStrings, new FakeAdapter()));
    }

    [Fact]
    public void Constructor_Null_Symbols_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BorrowedTickData(EmptyStreams, null!, EmptyMmList, EmptyStrings, new FakeAdapter()));
    }

    [Fact]
    public void Constructor_Length_Mismatch_Throws()
    {
        var stream = new IReadOnlyList<Tick>[] { Array.Empty<Tick>() };
        var symbols = new[] { "A", "B" };
        var mmList = new MemoryMappedTickList[] { new(_tempFile) };
        var paths = new[] { "A" };

        Assert.Throws<ArgumentException>(() =>
            new BorrowedTickData(stream, symbols, mmList, paths, new FakeAdapter()));
    }

    [Fact]
    public async Task DisposeAsync_Notifies_Adapter_For_Each_File()
    {
        var adapter = new FakeAdapter();
        var streams = new IReadOnlyList<Tick>[] { Array.Empty<Tick>(), Array.Empty<Tick>() };
        var symbols = new[] { "A", "B" };
        var mmLists = new MemoryMappedTickList[] { new(_tempFile), new(_tempFile) };
        var paths = new[] { "/path/a", "/path/b" };
        await using var data = new BorrowedTickData(streams, symbols, mmLists, paths, adapter);
        await data.DisposeAsync();
        Assert.Equal(2, adapter.NotifiedPaths.Count);
    }

    [Fact]
    public async Task DisposeAsync_Disposes_All_MemoryMappedTickLists()
    {
        BinaryDataMapper.WriteTicksToBinary(_tempFile, TestTicks);
        var mmList = new MemoryMappedTickList(_tempFile);
        var adapter = new FakeAdapter();
        var streams = new IReadOnlyList<Tick>[] { TestTicks };
        var mmLists = new[] { mmList };
        var paths = new[] { _tempFile };
        await using var data = new BorrowedTickData(streams, SymbolsX, mmLists, paths, adapter);
        await data.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => mmList[0]);
    }

    [Fact]
    public async Task DisposeAsync_Can_Be_Called_Twice()
    {
        await using var data = new BorrowedTickData(EmptyStreams, EmptyStrings, EmptyMmList, EmptyStrings, new FakeAdapter());
        await data.DisposeAsync();
        await data.DisposeAsync();
    }

    [Fact]
    public async Task Streams_And_Symbols_Preserved()
    {
        var streams = new IReadOnlyList<Tick>[] { TestTicks };
        var mmLists = new MemoryMappedTickList[] { new(_tempFile) };
        var paths = new[] { "/tmp/test.chrs" };
        await using var data = new BorrowedTickData(streams, SymbolsBtc, mmLists, paths, new FakeAdapter());
        Assert.Single(data.Streams);
        Assert.Equal(TestTicks, data.Streams[0]);
        Assert.Equal("BTCUSDT", data.Symbols[0]);

        await data.DisposeAsync();
    }
}
#pragma warning restore CA2007
