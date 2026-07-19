// -----------------------------------------------------------------------------
// <copyright file="BrokerParityTests.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Threading;
using Razor.Core.Kernel.Brokers;
using Razor.Core.Kernel.Clock;
using Razor.Core.Kernel.Telemetry;
using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.Adapter;

namespace Razor.Core.Kernel.UnitTests;

/// <summary>
/// Parity tests for the simulated and live brokers. These encode the CORRECT
/// expected behaviour for three known correctness bugs (Principle 5: sim/live
/// formulas must be identical). They use a mock <see cref="IMarketCalculator"/>
/// and a fixed conversion rate to validate cross‑currency margin/PnL math.
/// </summary>
public sealed class BrokerParityTests
{
    private const double ConversionRate = 2.0; // 1 unit of symbol quote currency = 2.0 account currency.

    private static SymbolProperties CreateSymbol(string marginCurrency = "USD")
    {
        return new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Isolated,
            PendingTrigger = PendingOrderTriggerMode.UseMidPrice,
            MarginCurrency = marginCurrency,
            ContractSize = 100_000,
            TickSize = 0.0001,
            TickValue = 10,
            MinVolume = 0.01,
            MaxLeverage = 500,
            SwapLong = 0,
            SwapShort = 0,
            SwapRolloverHourUtc = 0,
            TripleSwapDayMultiplier = 3,
            FundingRate = 0,
            InitialMarginRate = 0.01,
            MaintenanceMarginRate = 0.005,
            MakerFeeRate = 0.0001,
            TakerFeeRate = 0.0002
        };
    }

    private sealed class FixedCalculator : IMarketCalculator
    {
        public double NormalizeVolume(SymbolProperties p, double v) => Math.Max(0.01, Math.Round(v, 2));
        public double NormalizePrice(SymbolProperties p, double price) => Math.Round(price, 4);
        public double CalculateRequiredMargin(SymbolProperties p, double price, double volume, double leverage)
            => (price * volume * p.ContractSize) / leverage;
        public double CalculatePnL(SymbolProperties p, double entry, double current, double volume, OrderType type)
        {
            double dir = type == OrderType.Buy ? 1 : -1;
            return dir * (current - entry) * volume * p.ContractSize;
        }
        public double CalculateCommission(SymbolProperties p, double price, double volume)
            => price * volume * p.ContractSize * p.TakerFeeRate;
        public double CalculateSwap(SymbolProperties p, double v, OrderType t, long o, long c) => 0;
        public double CalculateFunding(SymbolProperties p, double v, double o, OrderType t, long now, long last) => 0;
        public bool IsPendingOrderTriggered(SymbolProperties p, OrderType pt, double bid, double ask, double orderPrice) => false;
        public double CalculateHoldingCost(SymbolProperties p, double v, double o, OrderType t, long from, long to) => 0;
        public double CalculateSlippage(SymbolProperties p, OrderType t, double v, double price) => 0;
    }

    private sealed class FixedConverter : ICurrencyConverter
    {
        public Task<double> GetConversionRateAsync(string symbol, string accountCurrency, CancellationToken ct = default)
            => Task.FromResult(ConversionRate);
    }

    // ── Bug (a): SimulatedBroker open‑commission must be charged at close ──────

    [Fact]
    public async Task SimulatedBroker_ChargesOpenCommissionAtClose()
    {
        var calc = new FixedCalculator();
        var symbol = CreateSymbol();
        var specs = new Dictionary<string, SymbolProperties> { ["EURUSD"] = symbol };
        var clock = new TickClock();

        var broker = new SimulatedBroker(calc, specs, initialBalance: 10_000, leverage: 100, clock,
            currencyConverter: new FixedConverter(), accountCurrency: "USD");

        const double openPrice = 1.1000;
        const double volume = 0.1;
        var tick = new Tick(0, openPrice, openPrice, 1, true);
        await broker.OnTickAsync("EURUSD", tick);

        var open = await broker.ExecuteMarketOrderAsync("EURUSD", OrderType.Buy, volume, 0, 0, "");
        Assert.True(open.Success);

        double openCommission = calc.CalculateCommission(symbol, openPrice, volume);
        double balanceAfterOpen = broker.Balance;

        var closeTick = new Tick(10, openPrice, openPrice, 1, true);
        await broker.OnTickAsync("EURUSD", closeTick);
        await broker.CloseAllAsync("EURUSD");

        double expectedBalance = balanceAfterOpen - openCommission - openCommission;
        Assert.Equal(expectedBalance, broker.Balance, 6);
    }

    // ── Bug (b): SimulatedBroker _marginUsed currency normalization ────────────

    [Fact]
    public async Task SimulatedBroker_NormalizesMarginUsedToAccountCurrency()
    {
        var calc = new FixedCalculator();
        var symbol = CreateSymbol();
        var specs = new Dictionary<string, SymbolProperties> { ["EURUSD"] = symbol };
        var clock = new TickClock();

        var broker = new SimulatedBroker(calc, specs, initialBalance: 100_000, leverage: 100, clock,
            currencyConverter: new FixedConverter(), accountCurrency: "USD");

        const double openPrice = 1.1000;
        const double volume = 0.1;
        var tick = new Tick(0, openPrice, openPrice, 1, true);
        await broker.OnTickAsync("EURUSD", tick);

        var open = await broker.ExecuteMarketOrderAsync("EURUSD", OrderType.Buy, volume, 0, 0, "");
        Assert.True(open.Success);

        double requiredMargin = calc.CalculateRequiredMargin(symbol, openPrice, volume, 100);
        double expectedMargin = requiredMargin * ConversionRate;

        Assert.Equal(expectedMargin, broker.MarginUsed, 6);

        var tick2 = new Tick(20, openPrice + 0.0010, openPrice + 0.0010, 1, true);
        await broker.OnTickAsync("EURUSD", tick2);
        Assert.Equal(expectedMargin, broker.MarginUsed, 6);

        Assert.Equal(broker.Equity - expectedMargin, broker.FreeMargin, 6);
    }

    // ── Bug (c): LiveBroker persists _marginUsed mid‑tick ─────────────────────

    [Fact]
    public async Task LiveBroker_PersistsMarginUsedMidTick()
    {
        var calc = new FixedCalculator();
        var symbol = CreateSymbol();
        var position = new Position
        {
            Ticket = 1,
            Symbol = "EURUSD",
            Type = OrderType.Buy,
            Volume = 0.1,
            OpenPrice = 1.1000,
            OpenTime = 0,
            ClosePrice = 0,
            CloseTime = 0,
            Commission = 0,
            Swap = 0,
            Profit = 0,
            ReturnPct = 0,
            AccountEquityAtOpen = 100_000,
            Leverage = 100
        };

        var adapter = new MockAdapter(calc, symbol, position, balance: 100_000, equity: 100_000);

        await using var metrics = new CoreMetrics("broker-parity-test");
        await using var broker = new LiveBroker(adapter, magicNumber: 1, leverage: 100, new TickClock(), new SystemClock(), metrics,
            currencyConverter: new FixedConverter(), accountCurrency: "USD");

        await broker.InitializeLiveStateAsync(CancellationToken.None);

        double expectedMargin = calc.CalculateRequiredMargin(symbol, position.OpenPrice, position.Volume, 100) * ConversionRate;
        Assert.Equal(expectedMargin, broker.MarginUsed, 6);

        var marginField = typeof(LiveBroker).GetField("_marginUsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        marginField.SetValue(broker, 0.0);
        Assert.Equal(0.0, broker.MarginUsed, 6);

        broker.OnTickReceived("EURUSD", new Tick(30, 1.1010, 1.1010, 1, true));

        bool refreshed = SpinWait.SpinUntil(() => Math.Abs(broker.MarginUsed - expectedMargin) < 1e-6, TimeSpan.FromSeconds(2));
        Assert.True(refreshed, "LiveBroker did not persist _marginUsed during the mid-tick update.");
        Assert.Equal(expectedMargin, broker.MarginUsed, 6);
    }

    private sealed class MockAdapter : IAdapterCapability
    {
        private readonly IMarketCalculator _calc;
        private readonly SymbolProperties _spec;
        private readonly IReadOnlyList<Position> _positions;
        private readonly double _balance;
        private readonly double _equity;

        public MockAdapter(IMarketCalculator calc, SymbolProperties spec, Position position, double balance, double equity)
        {
            _calc = calc;
            _spec = spec;
            _positions = new List<Position> { position };
            _balance = balance;
            _equity = equity;
        }

        public string Name => "Mock";
        public IMarketCalculator Calculator => _calc;
        public bool IsConnected => true;
        public bool SupportsHistoricalData => false;
        public bool SupportsLiveData => true;
        public bool SupportsExecution => true;

        public Task<bool> ConnectAsync(CancellationToken ct) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(HistoricalDataRequest r, CancellationToken ct)
            => Task.FromResult<HistoricalDataResponse>(null!);
        public Task DeleteHistoryFileAsync(string p) => Task.CompletedTask;
        public Task NotifyFileSafeToDeleteAsync(string p) => Task.CompletedTask;
        public Task SubscribeAsync(string s) => Task.CompletedTask;
        public Task UnsubscribeAsync(string s) => Task.CompletedTask;
        public event Action<string, Tick>? OnTickReceived = delegate { };
        public Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest r)
            => Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<AdapterOrderResponse> ModifyOrderAsync(long t, double? sl = null, double? tp = null, double? pr = null)
            => Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<AdapterOrderResponse> ClosePositionAsync(long t, double? v = null)
            => Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<AdapterOrderResponse> CancelAsync(long t)
            => Task.FromResult(new AdapterOrderResponse { Success = true });
        public Task<(double Balance, double Equity)> GetAccountInfoAsync(CancellationToken ct = default)
            => Task.FromResult((_balance, _equity));
        public Task<IReadOnlyList<Position>> GetActivePositionsAsync() => Task.FromResult(_positions);
        public Task<IReadOnlyList<Order>> GetPendingOrdersAsync() => Task.FromResult<IReadOnlyList<Order>>(Array.Empty<Order>());
        public Task<SymbolProperties?> GetSymbolPropertiesAsync(string s, CancellationToken ct = default)
            => Task.FromResult<SymbolProperties?>(_spec);
        public event Action<ExecutionReport>? OnExecutionUpdate = delegate { };
        public TimeFrame[]? GetSupportedTimeframes(string s) => null;
    }
}
