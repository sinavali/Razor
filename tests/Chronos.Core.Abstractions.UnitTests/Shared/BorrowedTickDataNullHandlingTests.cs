#pragma warning disable CA2007 // ConfigureAwait not needed in test methods
using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public sealed class BorrowedTickDataNullHandlingTests
{
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
    public async Task Constructor_Null_Streams_Does_Not_Throw()
    {
        await using var data = new BorrowedTickData(
            null!,
            Array.Empty<string>(),
            Array.Empty<MemoryMappedTickList>(),
            Array.Empty<string>(),
            new FakeAdapter());

        Assert.NotNull(data);
    }

    [Fact]
    public async Task Constructor_Null_Symbols_Does_Not_Throw()
    {
        await using var data = new BorrowedTickData(
            Array.Empty<IReadOnlyList<Tick>>(),
            null!,
            Array.Empty<MemoryMappedTickList>(),
            Array.Empty<string>(),
            new FakeAdapter());

        Assert.NotNull(data);
    }
}
#pragma warning restore CA2007
