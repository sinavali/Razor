using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public sealed class BorrowedTickData_IntegrationTests : IDisposable
{
    private readonly string _tempFile1;
    private readonly string _tempFile2;
    private readonly List<string> _tempFiles = new();
    private static readonly string[] symbols = new[] { "X" };
    private static readonly string[] symbolsArray = new[] { "A", "B" };
    private static readonly string[] symbolsArray0 = new[] { "A" };
    private static readonly string[] symbolsArray1 = new[] { "BTCUSDT" };
    private static readonly string[] symbolsArray2 = new[] { "X" };

    public BorrowedTickData_IntegrationTests()
    {
        _tempFile1 = Path.GetTempFileName();
        _tempFile2 = Path.GetTempFileName();
        for (int i = 0; i < 5; i++)
        {
            _tempFiles.Add(Path.GetTempFileName());
        }
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile1))
        {
            File.Delete(_tempFile1);
        }

        if (File.Exists(_tempFile2))
        {
            File.Delete(_tempFile2);
        }

        foreach (var f in _tempFiles)
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }

        GC.SuppressFinalize(this);
    }

    private sealed class CountingAdapter : IAdapterCapability
    {
        public int NotifyCount;
        public string Name => "Counter";
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
            NotifyCount++;
            return Task.CompletedTask;
        }
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
    public async Task Disposes_Multiple_MemoryMappedFiles_And_Notifies_Adapter()
    {
        var ticks1 = new Tick[] { new(1, 1, 2, 3), new(2, 2, 3, 4) };
        var ticks2 = new Tick[] { new(3, 3, 4, 5) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile1, ticks1);
        BinaryDataMapper.WriteTicksToBinary(_tempFile2, ticks2);

        var mm1 = new MemoryMappedTickList(_tempFile1);
        var mm2 = new MemoryMappedTickList(_tempFile2);
        var adapter = new CountingAdapter();
        var data = new BorrowedTickData(
            new IReadOnlyList<Tick>[] { ticks1, ticks2 },
            symbolsArray,
            new[] { mm1, mm2 },
            new[] { _tempFile1, _tempFile2 },
            adapter);

        await data.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => mm1[0]);
        Assert.Throws<ObjectDisposedException>(() => mm2[0]);
        Assert.Equal(2, adapter.NotifyCount);
    }

    [Fact]
    public async Task DisposeAsync_Can_Be_Called_Multiple_Times()
    {
        var ticks = new Tick[] { new(1, 1, 2, 3) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile1, ticks);
        var mm = new MemoryMappedTickList(_tempFile1);
        var adapter = new CountingAdapter();
        var data = new BorrowedTickData(
            new IReadOnlyList<Tick>[] { ticks },
            symbols,
            new[] { mm },
            new[] { _tempFile1 },
            adapter);

        await data.DisposeAsync();
        await data.DisposeAsync();
        Assert.Equal(2, adapter.NotifyCount);
    }

    [Fact]
    public async Task Large_Multi_Symbol_Dispose_Notifies_All_Files()
    {
        var adapter = new CountingAdapter();
        int count = 5;
        var streams = new IReadOnlyList<Tick>[count];
        var syms = new string[count];
        var mmLists = new MemoryMappedTickList[count];
        var paths = new string[count];

        for (int i = 0; i < count; i++)
        {
            var ticks = new Tick[1000];
            for (int j = 0; j < ticks.Length; j++)
            {
                ticks[j] = new Tick(j, j, j, j);
            }

            BinaryDataMapper.WriteTicksToBinary(_tempFiles[i], ticks);
            streams[i] = ticks;
            syms[i] = $"SYM{i}";
            mmLists[i] = new MemoryMappedTickList(_tempFiles[i]);
            paths[i] = _tempFiles[i];
        }

        var data = new BorrowedTickData(streams, syms, mmLists, paths, adapter);
        await data.DisposeAsync();
        Assert.Equal(count, adapter.NotifyCount);
        for (int i = 0; i < count; i++)
        {
            Assert.Throws<ObjectDisposedException>(() => mmLists[i][0]);
        }
    }

    [Fact]
    public void Constructor_Streams_Symbols_Length_Mismatch_Throws()
    {
        var ticks = new Tick[] { new(1, 1, 2, 3) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile1, ticks);
        using var mm = new MemoryMappedTickList(_tempFile1);

        Assert.Throws<ArgumentException>(() => new BorrowedTickData(
            new IReadOnlyList<Tick>[] { ticks, ticks },
            symbolsArray0,
            new MemoryMappedTickList[] { mm, mm },
            new[] { _tempFile1, _tempFile1 },
            new CountingAdapter()));
    }

#pragma warning disable CA2007
    [Fact]
    public async Task Constructor_Streams_And_Symbols_Are_Preserved()
    {
        var ticks = new Tick[] { new(1, 1, 2, 3), new(2, 3, 4, 5) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile1, ticks);
        var mm = new MemoryMappedTickList(_tempFile1);

        await using var data = new BorrowedTickData(
            new IReadOnlyList<Tick>[] { ticks },
            symbolsArray1,
            new[] { mm },
            new[] { _tempFile1 },
            new CountingAdapter());

        Assert.Single(data.Streams);
        Assert.Equal(ticks, data.Streams[0]);
        Assert.Equal("BTCUSDT", data.Symbols[0]);
    }
#pragma warning restore CA2007

    [Fact]
    public void Constructor_MappedLists_Count_Mismatch_Throws()
    {
        var ticks = new Tick[] { new(1, 1, 2, 3) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile1, ticks);
        using var mm = new MemoryMappedTickList(_tempFile1);

        Assert.Throws<ArgumentException>(() => new BorrowedTickData(
            new IReadOnlyList<Tick>[] { ticks },
            symbolsArray2,
            Array.Empty<MemoryMappedTickList>(),  // count 0 != streams.Length 1
            new[] { _tempFile1 },
            new CountingAdapter()));
    }
}
