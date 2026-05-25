using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Adapters;

public class AdapterDefaultMethodTests
{
    private sealed class TestAdapter : IAdapter
    {
        public string AdapterName => "Test";
        public IMarketCalculator Calculator => null!;
        public bool IsConnected => false;
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
    public void GetSupportedTimeframes_Default_Returns_Null()
    {
        IAdapter adapter = new TestAdapter();
        var result = adapter.GetSupportedTimeframes("EURUSD");
        Assert.Null(result);
    }
}
