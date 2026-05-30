using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public sealed class BorrowedTickData_IntegrationTests : IDisposable
{
    private readonly string _tempFile1;
    private readonly string _tempFile2;
    private static readonly string[] symbols = new[] { "A", "B" };

    public BorrowedTickData_IntegrationTests()
    {
        _tempFile1 = Path.GetTempFileName();
        _tempFile2 = Path.GetTempFileName();
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

        GC.SuppressFinalize(this);
    }

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
        public Task<IReadOnlyList<Position>> GetActivePositionsAsync() => Task.FromResult((IReadOnlyList<Position>)Array.Empty<Position>());
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync() => Task.FromResult((IReadOnlyList<Order>)Array.Empty<Order>());
        public Task<SymbolProperties?> GetSymbolPropertiesAsync(string s, CancellationToken ct) => Task.FromResult<SymbolProperties?>(null);
    }

    [Fact]
    public async Task Disposes_Multiple_MemoryMappedFiles_And_Notifies_Adapter()
    {
        var ticks1 = new Tick[] { new Tick(1, 1, 2, 3), new Tick(2, 2, 3, 4) };
        var ticks2 = new Tick[] { new Tick(3, 3, 4, 5) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile1, ticks1);
        BinaryDataMapper.WriteTicksToBinary(_tempFile2, ticks2);

        var mm1 = new MemoryMappedTickList(_tempFile1);
        var mm2 = new MemoryMappedTickList(_tempFile2);

        var adapter = new FakeAdapter();
        var data = new BorrowedTickData(
            new IReadOnlyList<Tick>[] { ticks1, ticks2 },
            symbols,
            new[] { mm1, mm2 },
            new[] { _tempFile1, _tempFile2 },
            adapter);

        await data.DisposeAsync();

        // After dispose, accessing mm should throw
        Assert.Throws<ObjectDisposedException>(() => mm1[0]);
        Assert.Throws<ObjectDisposedException>(() => mm2[0]);
    }
}
