// -----------------------------------------------------------------------------
// <copyright file="GoldenDeterminismTests.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Razor.Core.Kernel.Backtesting;
using Razor.Core.Kernel.Clock;
using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.Strategy;

namespace Razor.Determinism.Tests;

/// <summary>
/// Golden‑gate determinism test (Principle 12): a fixed‑seed backtest run twice
/// with identical inputs must produce byte‑identical trade history. No wall‑clock
/// or other nondeterministic source is used inside the run.
/// </summary>
public sealed class GoldenDeterminismTests
{
    private const int MasterSeed = 123456789;
    private const int GeneInitializationSeed = 42;

    [Fact]
    public void Golden_Backtest_IsDeterministic_AcrossRuns()
    {
        var inputA = BuildInput();
        var inputB = BuildInput();

        var runner = new BacktestRunner();
        var resultA = runner.RunAsync(inputA, CancellationToken.None).GetAwaiter().GetResult();
        var resultB = runner.RunAsync(inputB, CancellationToken.None).GetAwaiter().GetResult();

        var hashA = HashHistory(resultA.History);
        var hashB = HashHistory(resultB.History);

        Assert.Equal(hashA, hashB);
    }

    private static BacktestInput BuildInput()
    {
        const string symbol = "EURUSD";

        var spec = new SymbolProperties
        {
            AssetClass = AssetClass.Forex,
            MarginMode = MarginMode.Isolated,
            PendingTrigger = PendingOrderTriggerMode.UseBidForBuy,
            MarginCurrency = "USD",
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

        var bars = BuildBars(symbol);
        var ticks = TickSynthesizer.BarsToTicks(bars, ticksPerBar: 4, seed: MasterSeed);

        var symbolSpecs = new Dictionary<string, SymbolProperties> { [symbol] = spec };
        var strategySpec = new StrategySpecification
        {
            InitialBalance = 10_000,
            Leverage = 100,
            RequestedSymbols = ImmutableArray.Create(
                new SymbolRequest(symbol, ImmutableArray.Create(TimeFrame.Tick)))
        };

        var execSpec = new ExecutionSpecification
        {
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            WarmupWindowCount = 0,
            MaxOpenPositions = 10,
            StopOutLevel = 0.5,
            GeneInitializationSeed = GeneInitializationSeed
        };

        return new BacktestInput
        {
            TickStreams = new IReadOnlyList<Tick>[] { ticks },
            Symbols = new[] { symbol },
            Strategy = new DeterministicStrategy(),
            StrategySpecification = strategySpec,
            ExecutionSpecification = execSpec,
            MarketCalculator = new FixedCalculator(),
            SymbolProperties = symbolSpecs,
            GeneInitializationSeed = GeneInitializationSeed
        };
    }

    private static Bar[] BuildBars(string symbol)
    {
        var bars = new Bar[200];
        double price = 1.1000;
        long t = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        long step = TimeSpan.TicksPerMinute;
        for (int i = 0; i < bars.Length; i++)
        {
            double open = price;
            double drift = Math.Sin(i * 0.1) * 0.0005;
            double close = open + drift;
            double high = Math.Max(open, close) + 0.0002;
            double low = Math.Min(open, close) - 0.0002;
            bars[i] = new Bar(t, open, high, low, close, 1000, t + step - 1);
            price = close;
            t += step;
        }

        return bars;
    }

    private static string HashHistory(IReadOnlyList<Position> history)
    {
        var sb = new StringBuilder();
        foreach (var p in history.OrderBy(x => x.CloseTime).ThenBy(x => x.Ticket))
        {
            sb.Append(CultureInfo.InvariantCulture, $"{p.Ticket}|{p.Symbol}|{(int)p.Type}|")
              .Append(CultureInfo.InvariantCulture, $"{p.Volume.ToString(CultureInfo.InvariantCulture)}|")
              .Append(CultureInfo.InvariantCulture, $"{p.OpenPrice.ToString(CultureInfo.InvariantCulture)}|")
              .Append(CultureInfo.InvariantCulture, $"{p.OpenTime}|")
              .Append(CultureInfo.InvariantCulture, $"{p.ClosePrice.ToString(CultureInfo.InvariantCulture)}|")
              .Append(CultureInfo.InvariantCulture, $"{p.CloseTime}|")
              .Append(CultureInfo.InvariantCulture, $"{p.Commission.ToString(CultureInfo.InvariantCulture)}|")
              .Append(CultureInfo.InvariantCulture, $"{p.Swap.ToString(CultureInfo.InvariantCulture)}|")
              .Append(CultureInfo.InvariantCulture, $"{p.Profit.ToString(CultureInfo.InvariantCulture)}|")
              .Append(CultureInfo.InvariantCulture, $"{p.ReturnPct.ToString(CultureInfo.InvariantCulture)};");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Deterministic, dependency‑free strategy: opens a single buy on the first tick
    /// and closes it once price has moved a fixed amount. No external/adapter deps.
    /// </summary>
    private sealed class DeterministicStrategy : StrategyBase
    {
        private bool _opened;
        private double _entryPrice;

        public override void OnTick(string symbol, Tick tick)
        {
            if (!_opened)
            {
                _opened = true;
                _entryPrice = tick.Ask;
                BuyAsync(symbol, 0.1).GetAwaiter().GetResult();
                return;
            }

            double target = _entryPrice + 0.0020;
            if (tick.Bid >= target)
            {
                CloseAllAsync(symbol).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>Stateless, deterministic financial calculator (no external deps).</summary>
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
}
