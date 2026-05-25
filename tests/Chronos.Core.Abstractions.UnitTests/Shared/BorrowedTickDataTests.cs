using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public sealed class BorrowedTickDataTests : IDisposable
{
    private readonly string _tempFile;

    public BorrowedTickDataTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
        GC.SuppressFinalize(this);
    }

#pragma warning disable CS0067
    private sealed class FakeAdapter : IAdapter
    {
        public string AdapterName => "Fake";
        public IMarketCalculator Calculator => null!;
        public bool IsConnected => false;
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

        public List<string> NotifiedPaths { get; } = new();

        public Task SubscribeAsync(string symbol) => Task.CompletedTask;
        public Task UnsubscribeAsync(string symbol) => Task.CompletedTask;
        public event Action<string, Tick>? OnTickReceived;
        public Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest r) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ModifyOrderAsync(long t, double? sl, double? tp, double? price) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> ClosePositionAsync(long t, double? v) => Task.FromResult(new AdapterOrderResponse());
        public Task<AdapterOrderResponse> CancelAsync(long t) => Task.FromResult(new AdapterOrderResponse());
        public Task<(double, double)> GetAccountInfoAsync(CancellationToken ct) => Task.FromResult((0.0, 0.0));
        public Task<IReadOnlyList<Position>> GetActivePositionsAsync() => Task.FromResult((IReadOnlyList<Position>)Array.Empty<Position>());
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync() => Task.FromResult((IReadOnlyList<Order>)Array.Empty<Order>());
        public Task<SymbolProperties?> GetSymbolPropertiesAsync(string s, CancellationToken ct) => Task.FromResult<SymbolProperties?>(null);
        public event Action<ExecutionReport>? OnExecutionUpdate;
    }
#pragma warning restore CS0067

    private static readonly Tick[] _testTicks = { new Tick(1, 2, 3, 4) };
    private static readonly IReadOnlyList<Tick>[] EmptyStreams = Array.Empty<IReadOnlyList<Tick>>();
    private static readonly string[] EmptyStrings = Array.Empty<string>();
    private static readonly MemoryMappedTickList[] EmptyMmList = Array.Empty<MemoryMappedTickList>();
    private static readonly string[] symbols = new[] { "X" };
    private static readonly string[] symbolsArray = new[] { "BTCUSDT" };

    [Fact]
    public async Task DisposeAsync_Notifies_Adapter_For_Each_File()
    {
        var adapter = new FakeAdapter();
        var filePaths = new[] { "/path/a", "/path/b" };
        var data = new BorrowedTickData(
            EmptyStreams,
            EmptyStrings,
            EmptyMmList,
            filePaths,
            adapter);

        await data.DisposeAsync();

        Assert.Equal(2, adapter.NotifiedPaths.Count);
        Assert.Contains("/path/a", adapter.NotifiedPaths);
        Assert.Contains("/path/b", adapter.NotifiedPaths);
    }

    [Fact]
    public async Task DisposeAsync_Disposes_All_MemoryMappedTickLists()
    {
        BinaryDataMapper.WriteTicksToBinary(_tempFile, _testTicks);
        var mmList = new MemoryMappedTickList(_tempFile);

        var adapter = new FakeAdapter();
        var data = new BorrowedTickData(
            new IReadOnlyList<Tick>[] { _testTicks },
            symbols,
            new[] { mmList },
            new[] { _tempFile },
            adapter);

        await data.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => mmList[0]);
    }

    [Fact]
    public async Task DisposeAsync_Streams_And_Symbols_Are_Preserved()
    {
        var data = new BorrowedTickData(
            new[] { _testTicks },
            symbolsArray,
            EmptyMmList,
            EmptyStrings,
            new FakeAdapter());

        Assert.Single(data.Streams);
        Assert.Equal(_testTicks, data.Streams[0]);
        Assert.Equal("BTCUSDT", data.Symbols[0]);

        await data.DisposeAsync();
    }
}
