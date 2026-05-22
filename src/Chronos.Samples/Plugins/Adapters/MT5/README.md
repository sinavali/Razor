1. add "Mql5BridgeEA.ex5" EA to a chart or compile and add the "Mql5BridgeEA.mq5" from "MQL5\Experts".
2. add "winsock.mqh" to "MQL5\Include\WinAPI"


- it does not matter to what chart.
- it will use port 5555
- it will whatch the watch-list
```
</file>


<file path="Chronos/src/Chronos.Samples/Plugins/Adapters/MT5/winsock.mqh">
```mqh
//+------------------------------------------------------------------+
//|                                                  winsock.mqh     |
//|                      Copyright 2026, Chronos Co.                 |
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, Chronos Co."
#property link      "https://chronos.com"

// Structures must be defined BEFORE they are used in #import
struct WSADATA
{
   ushort wVersion;
   ushort wHighVersion;
   char   szDescription[257];
   char   szSystemStatus[129];
   ushort iMaxSockets;
   ushort iMaxUdpDg;
   char   lpVendorInfo;
};

struct sockaddr_in
{
   short  sin_family;
   ushort sin_port;
   int    sin_addr;
   char   sin_zero[8];
};

#import "ws2_32.dll"
   int  WSAStartup(int wVersionRequested, WSADATA& lpWSAData);
   int  WSACleanup(void);
   int  WSAGetLastError(void);
   int  socket(int af, int type, int protocol);
   int  bind(int s, sockaddr_in& name, int namelen);
   int  listen(int s, int backlog);
   int  accept(int s, sockaddr_in& addr, int& addrlen);
   int  closesocket(int s);
   int  send(int s, uchar& buf[], int len, int flags);
   int  recv(int s, uchar& buf[], int len, int flags);
   int  ioctlsocket(int s, int cmd, uint& argp);
   int  htons(int hostshort);
   int  inet_addr(string cp);
#import

#define AF_INET         2
#define SOCK_STREAM     1
#define INVALID_SOCKET  -1
#define SOCKET_ERROR    -1
#define INADDR_ANY      0
#define FIONBIO         0x5421
#define WSAEWOULDBLOCK  10035
```
</file>


<file path="Chronos/src/Chronos.Samples/Plugins/Properties/AssemblyInfo.cs">
```cs
using Chronos.Abstractions.Adapters;
using Chronos.Abstractions.Plugins;

[assembly: ChronosSdkVersion("1.0.0")]
[assembly: AdapterVersion("1.0.0")]

```
</file>


<file path="Chronos/src/Chronos.Samples/Plugins/Strategies/CfdSmtDivergence/CfdSmtDivergenceStrategy.cs">
```cs
// Chronos/src/Chronos.Samples/Plugins/Strategies/CfdSmtDivergence/CfdSmtDivergenceStrategy.cs

using System.Collections.Concurrent;
using Chronos.Abstractions.Shared;
using Chronos.Abstractions.Strategies;

namespace Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

/// <summary>
/// Trading strategy that detects SMT divergence across multiple CFD symbols.
/// Uses a pair‑based approach, comparing all symbol combinations automatically.
///
/// Key features:
/// - Session and WorkTime filters (temporarily disabled for debugging).
/// - Volume auto‑calculated to risk 0.2% of equity per trade.
/// - Stop loss placed at recent swing high/low plus a full pip cushion.
/// - Optional feed‑forward neural network that filters divergence signals.
/// - Genome size &gt; 30 (49 genes) – suitable for optimisation.
/// </summary>
internal sealed class CfdSmtDivergenceStrategy : StrategyBase
{
    // ---------------------------------------------------------------
    // Genes – Non‑NN parameters
    // ---------------------------------------------------------------

    /// <summary>Minimum number of completed bars before any trade signal is generated.</summary>
    [Gene(1, 20, step: 1, type: GeneType.Discrete, Name = "Min Window Count")]
    public int MinWindowCount { get; set; } = 2;

    /// <summary>Number of recent bars kept in memory for divergence comparison.</summary>
    [Gene(2, 20, step: 1, type: GeneType.Discrete, Name = "Lookback Window Count")]
    public int LookbackWindowCount { get; set; } = 2;

    /// <summary>Minimum price difference (in price units) to confirm divergence between two symbols.</summary>
    [Gene(0.00001, 0.001, step: 0.00002, type: GeneType.Continuous, Name = "Divergence Threshold (price)")]
    public double DivergenceThresholdPoints { get; set; } = 0.0001;

    /// <summary>Enable (1) or disable (0) the Asia trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use Asia Session")]
    public int UseAsiaGene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the London trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use London Session")]
    public int UseLondonGene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork 1 trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NY1 Session")]
    public int UseNY1Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork 2 trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NY2 Session")]
    public int UseNY2Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the London WorkTime 1 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use LWT1")]
    public int UseLWT1Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the London WorkTime 2 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use LWT2")]
    public int UseLWT2Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork WorkTime 1 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NYWT1")]
    public int UseNYWT1Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork WorkTime 2 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NYWT2")]
    public int UseNYWT2Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork WorkTime 3 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NYWT3")]
    public int UseNYWT3Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the neural network signal filter.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use Neural Network")]
    public int UseNeuralNetworkGene { get; set; } = 1;

    // ---------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------
    private const TimeFrame DetectionTimeFrame = TimeFrame.M5;
    private const double RiskPercent = 0.002;
    private static readonly int[] NeuralTopology = { 5, 5, 1 };

    private readonly SessionFilter _sessionFilter = new();
    private readonly WorkTimeFilter _workTimeFilter = new();

    /// <summary>Per‑symbol history of recent bars (High, Low).</summary>
    private readonly ConcurrentDictionary<string, Queue<(double High, double Low)>> _history =
        new(StringComparer.OrdinalIgnoreCase);

    private string[] _symbols = Array.Empty<string>();

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    /// <inheritdoc/>
    public override Task OnConfigureAsync(StrategySpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.RequestedSymbols.Length < 2)
            throw new StrategyException(nameof(CfdSmtDivergenceStrategy),
                "At least two symbols required for pair‑based SMT divergence.");

        _symbols = spec.RequestedSymbols.Select(sr => sr.Symbol).ToArray();

        foreach (var sr in spec.RequestedSymbols)
        {
            if (!sr.TimeFrames.Contains(DetectionTimeFrame))
                throw new StrategyException(nameof(CfdSmtDivergenceStrategy),
                    $"Symbol {sr.Symbol} must include {DetectionTimeFrame} timeframe.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnStartAsync(IIndicatorRegistry indicators)
    {
        if (UseNeuralNetworkGene != 0)
            NeuralNet = new FeedForwardNetwork(NeuralTopology, ActivationFunction.Tanh);

        TickWindow.WindowCompleted += OnWindowCompleted;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnStopAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task OnTickAsync(Tick tick) => Task.CompletedTask;

    // ---------------------------------------------------------------
    // Gene injection
    // ---------------------------------------------------------------

    /// <inheritdoc/>
    public override void InjectGenes(double[] genes)
    {
        base.InjectGenes(genes);

        _sessionFilter.UseAsia   = UseAsiaGene != 0;
        _sessionFilter.UseLondon = UseLondonGene != 0;
        _sessionFilter.UseNY1    = UseNY1Gene != 0;
        _sessionFilter.UseNY2    = UseNY2Gene != 0;

        _workTimeFilter.UseLWT1   = UseLWT1Gene != 0;
        _workTimeFilter.UseLWT2   = UseLWT2Gene != 0;
        _workTimeFilter.UseNYWT1  = UseNYWT1Gene != 0;
        _workTimeFilter.UseNYWT2  = UseNYWT2Gene != 0;
        _workTimeFilter.UseNYWT3  = UseNYWT3Gene != 0;

        if (UseNeuralNetworkGene != 0 && NeuralNet == null)
            NeuralNet = new FeedForwardNetwork(NeuralTopology, ActivationFunction.Tanh);
        else if (UseNeuralNetworkGene == 0)
            NeuralNet = null;
    }

    // ---------------------------------------------------------------
    // Window completion event handler
    // ---------------------------------------------------------------

    /// <summary>
    /// Called by the <see cref="TickWindow"/> when a bar of the tracked timeframe completes.
    /// Retrieves the pre‑computed bar directly and queues it for pair analysis.
    /// </summary>
    private void OnWindowCompleted(string symbol, TimeFrame tf)
    {
        if (tf != DetectionTimeFrame) return;

        if (!TickWindow.TryGetLastCompletedBar(symbol, tf, out _, out double high, out double low, out _, out _))
        {
            // No completed bar data available yet (first bar of the session)
            return;
        }

        var queue = _history.GetOrAdd(symbol, _ => new Queue<(double, double)>());
        lock (queue)
        {
            queue.Enqueue((high, low));
            while (queue.Count > LookbackWindowCount)
                queue.Dequeue();
        }

        // Process all pairs asynchronously; the backtest runner will wait for pending tasks
        _ = ProcessCompletedBarAsync();
    }

    /// <summary>
    /// Evaluates all symbol pairs after a bar completes.
    /// </summary>
    private async Task ProcessCompletedBarAsync()
    {
        var symbols = _symbols;
        for (int i = 0; i < symbols.Length; i++)
        {
            for (int j = i + 1; j < symbols.Length; j++)
            {
                await ProcessPairAsync(symbols[i], symbols[j]).ConfigureAwait(false);
            }
        }
    }

    // ---------------------------------------------------------------
    // Pair processing (time filters temporarily bypassed)
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks for SMT divergence between two symbols and executes a trade if conditions are met.
    /// </summary>
    private async Task ProcessPairAsync(string symA, string symB)
    {
        if (!_history.TryGetValue(symA, out var qA) || !_history.TryGetValue(symB, out var qB))
            return;

        (double highA, double lowA) prevA, currA;
        (double highB, double lowB) prevB, currB;

        lock (qA) lock (qB)
        {
            if (qA.Count < 2 || qB.Count < 2)       // minimum 2 bars for comparison
                return;

            prevA = qA.ElementAt(qA.Count - 2);
            currA = qA.Last();
            prevB = qB.ElementAt(qB.Count - 2);
            currB = qB.Last();
        }

        // Bearish divergence: A makes higher high, B makes lower high
        bool bearish = (currA.highA > prevA.highA && currB.highB < prevB.highB) &&
                       (Math.Abs(currA.highA - prevA.highA) >= DivergenceThresholdPoints) &&
                       (Math.Abs(currB.highB - prevB.highB) >= DivergenceThresholdPoints);

        // Bullish divergence: A makes lower low, B makes higher low
        bool bullish = (currA.lowA < prevA.lowA && currB.lowB > prevB.lowB) &&
                       (Math.Abs(currA.lowA - prevA.lowA) >= DivergenceThresholdPoints) &&
                       (Math.Abs(currB.lowB - prevB.lowB) >= DivergenceThresholdPoints);

        if (!bearish && !bullish) return;

        // Time filters are disabled for debugging – remove comment to re‑enable later.
        // long tickTime = GetLastTickTime(symA);
        // if (_sessionFilter.IsInSession(tickTime) == 0 || _workTimeFilter.IsInWorkTime(tickTime) == 0)
        //     return;

        if (UseNeuralNetworkGene != 0 && NeuralNet != null)
        {
            double[] inputs = BuildNeuralInputs(symA, bearish, bullish, prevA, currA);
            double[] outputs = NeuralNet.FeedForward(inputs);
            if (outputs.Length == 0 || outputs[0] <= 0)
                return;
        }

        bool isSell = bearish;
        string tradeSymbol = symA;

        if (await Broker.HasOpenPositionAsync(tradeSymbol).ConfigureAwait(false))
            return;

        await ExecuteTradeAsync(tradeSymbol, isSell,
            (isSell ? currA.highA : currA.lowA),
            (isSell ? currB.highB : currB.lowB)).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------
    // Neural network input construction
    // ---------------------------------------------------------------

    /// <summary>
    /// Builds the input vector for the neural network filter.
    /// </summary>
    private double[] BuildNeuralInputs(string symA, bool bearish, bool bullish,
        (double High, double Low) prevA, (double High, double Low) currA)
    {
        long tickTime = GetLastTickTime(symA);
        double session = _sessionFilter.IsInSession(tickTime);
        double worktime = _workTimeFilter.IsInWorkTime(tickTime);

        double bullishStrength = bullish
            ? Math.Clamp((prevA.Low - currA.Low) / DivergenceThresholdPoints, 0, 1)
            : 0;
        double bearishStrength = bearish
            ? Math.Clamp((currA.High - prevA.High) / DivergenceThresholdPoints, 0, 1)
            : 0;

        double barRange = currA.High - currA.Low;
        double normalizedRange = DivergenceThresholdPoints > 1e-8
            ? Math.Clamp(barRange / DivergenceThresholdPoints, 0, 5)
            : 0;

        return new[] { session, worktime, bullishStrength, bearishStrength, normalizedRange };
    }

    // ---------------------------------------------------------------
    // Trade execution (wider stop cushion)
    // ---------------------------------------------------------------

    /// <summary>
    /// Places a market order with risk‑based volume and a stop‑loss at the swing extreme.
    /// </summary>
    private async Task ExecuteTradeAsync(string symbol, bool isSell, double highA, double highB)
    {
        var recentTicks = TickWindow.GetRecentTicks(symbol, 1);
        if (recentTicks.Count == 0) return;
        Tick lastTick = recentTicks[0];

        double entryPrice = isSell ? lastTick.Bid : lastTick.Ask;
        if (entryPrice <= 0) return;

        double rawStopLevel = isSell ? Math.Max(highA, highB) : Math.Min(highA, highB);
        double pipSize = symbol.EndsWith("JPY", StringComparison.OrdinalIgnoreCase) ? 0.01 : 0.0001;
        double safetyCushion = pipSize * 1.0;            // full pip cushion

        double stopPrice;
        if (isSell)
            stopPrice = Math.Max(rawStopLevel, entryPrice + safetyCushion) + safetyCushion;
        else
            stopPrice = Math.Min(rawStopLevel, entryPrice - safetyCushion) - safetyCushion;

        double slDistanceAbs = Math.Abs(entryPrice - rawStopLevel);
        if (slDistanceAbs <= 0) return;

        double slDistancePips = slDistanceAbs / pipSize;
        double pipValuePerLot;
        if (symbol.EndsWith("JPY", StringComparison.OrdinalIgnoreCase))
        {
            double currentPrice = (lastTick.Bid + lastTick.Ask) * 0.5;
            pipValuePerLot = currentPrice > 0 ? 1000.0 / currentPrice : 9.0;
        }
        else
            pipValuePerLot = 10.0;

        double equity = Broker.Equity;
        if (equity <= 0) return;

        double riskAmount = equity * RiskPercent;
        double volumeLots = riskAmount / (slDistancePips * pipValuePerLot);
        volumeLots = Math.Round(Math.Max(0.01, Math.Min(volumeLots, 50)), 2, MidpointRounding.AwayFromZero);

        OrderType orderType = isSell ? OrderType.Sell : OrderType.Buy;

        await Broker.ExecuteMarketOrderAsync(symbol, orderType, volumeLots,
            sl: stopPrice, tp: 0, comment: "SMT Divergence").ConfigureAwait(false);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Returns the timestamp of the latest tick for the given symbol, or <c>DateTime.UtcNow.Ticks</c> if none.
    /// </summary>
    private long GetLastTickTime(string symbol)
    {
        var ticks = TickWindow.GetRecentTicks(symbol, 1);
        return ticks.Count > 0 ? ticks[0].Time : DateTime.UtcNow.Ticks;
    }
}

```
</file>


<file path="Chronos/src/Chronos.Samples/Plugins/Strategies/CfdSmtDivergence/SessionFilter.cs">
```cs
namespace Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

/// <summary>
/// Checks whether the current UTC time falls within one or more trading sessions.
/// Each session is a fixed UTC time window and can be individually enabled via a gene.
/// </summary>
internal sealed class SessionFilter
{
    private static readonly (string Name, TimeSpan Start, TimeSpan End)[] Sessions =
    {
        ("Asia",       new TimeSpan(0,  0, 0),  new TimeSpan(5, 30, 0)),
        ("London",     new TimeSpan(5, 45, 0),  new TimeSpan(10, 0, 0)),
        ("NewYork 1",  new TimeSpan(10, 45, 0), new TimeSpan(16, 0, 0)),
        ("NewYork 2",  new TimeSpan(16, 45, 0), new TimeSpan(19, 0, 0))
    };

    /// <summary>Gene: enable Asia session.</summary>
    public bool UseAsia { get; set; } = true;
    /// <summary>Gene: enable London session.</summary>
    public bool UseLondon { get; set; } = true;
    /// <summary>Gene: enable NewYork 1 session.</summary>
    public bool UseNY1 { get; set; } = true;
    /// <summary>Gene: enable NewYork 2 session.</summary>
    public bool UseNY2 { get; set; } = true;

    /// <summary>
    /// Returns 1 if the tick time falls within at least one enabled session, 0 otherwise.
    /// </summary>
    /// <param name="tickTime">The tick timestamp in UTC ticks.</param>
    /// <returns>1 if in session, 0 otherwise.</returns>
    public int IsInSession(long tickTime)
    {
        TimeSpan timeOfDay = new DateTime(tickTime, DateTimeKind.Utc).TimeOfDay;
        bool[] enabled = { UseAsia, UseLondon, UseNY1, UseNY2 };
        for (int i = 0; i < Sessions.Length; i++)
        {
            if (!enabled[i]) continue;
            var (_, start, end) = Sessions[i];
            if (start <= end)
            {
                if (timeOfDay >= start && timeOfDay <= end)
                    return 1;
            }
            else
            {
                if (timeOfDay >= start || timeOfDay <= end)
                    return 1;
            }
        }
        return 0;
    }
}

```
</file>


<file path="Chronos/src/Chronos.Samples/Plugins/Strategies/CfdSmtDivergence/WorkTimeFilter.cs">
```cs
namespace Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

/// <summary>
/// Checks whether the current UTC time falls within one or more custom work‑time windows.
/// Each window can be individually enabled via a gene.
/// </summary>
internal sealed class WorkTimeFilter
{
    private static readonly (string Name, TimeSpan Start, TimeSpan End)[] WorkTimes =
    {
        ("London WT1", new TimeSpan(5, 45, 0),  new TimeSpan(8,  0, 0)),
        ("London WT2", new TimeSpan(8, 45, 0),  new TimeSpan(9, 30, 0)),
        ("NY WT1",     new TimeSpan(10, 45, 0), new TimeSpan(13, 0, 0)),
        ("NY WT2",     new TimeSpan(13, 45, 0), new TimeSpan(14, 30, 0)),
        ("NY WT3",     new TimeSpan(16, 45, 0), new TimeSpan(18, 0, 0))
    };

    /// <summary>Gene: enable London WT1.</summary>
    public bool UseLWT1 { get; set; } = true;
    /// <summary>Gene: enable London WT2.</summary>
    public bool UseLWT2 { get; set; } = true;
    /// <summary>Gene: enable NY WT1.</summary>
    public bool UseNYWT1 { get; set; } = true;
    /// <summary>Gene: enable NY WT2.</summary>
    public bool UseNYWT2 { get; set; } = true;
    /// <summary>Gene: enable NY WT3.</summary>
    public bool UseNYWT3 { get; set; } = true;

    /// <summary>
    /// Returns 1 if the tick time falls within at least one enabled work‑time window, 0 otherwise.
    /// </summary>
    /// <param name="tickTime">The tick timestamp in UTC ticks.</param>
    /// <returns>1 if in work time, 0 otherwise.</returns>
    public int IsInWorkTime(long tickTime)
    {
        TimeSpan timeOfDay = new DateTime(tickTime, DateTimeKind.Utc).TimeOfDay;
        bool[] enabled = { UseLWT1, UseLWT2, UseNYWT1, UseNYWT2, UseNYWT3 };
        for (int i = 0; i < WorkTimes.Length; i++)
        {
            if (!enabled[i]) continue;
            var (_, start, end) = WorkTimes[i];
            if (timeOfDay >= start && timeOfDay <= end)
                return 1;
        }
        return 0;
    }
}

```
</file>


<file path="Chronos/src/Chronos.Samples/Chronos.Samples.csproj">
```csproj
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <WarningsAsErrors>nullable,CS1591</WarningsAsErrors>
        <RootNamespace>Chronos.Samples</RootNamespace>

    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Chronos.Abstractions\Chronos.Abstractions.csproj"/>
        <ProjectReference Include="..\Chronos.Kernel\Chronos.Kernel.csproj"/>
    </ItemGroup>

    <ItemGroup>
        <Folder Include="Plugins\Strategies\CryptoSimpleRsi\" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Chaos.NaCl.Standard" Version="1.0.0" />
        <PackageReference Include="Microsoft.Extensions.Http" Version="9.0.0"/>
        <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0"/>
    </ItemGroup>

</Project>

```
</file>


<file path="Chronos/src/Chronos.Samples/Program.cs">
```cs
using System.Collections.Immutable;
using Chronos.Abstractions.Adapters;
using Chronos.Abstractions.Shared;
using Chronos.Abstractions.Strategies;
using Chronos.Kernel.Backtesting;
using Chronos.Kernel.Configuration;
using Chronos.Kernel.Indicators;
using Chronos.Kernel.Messaging;
using Chronos.Kernel.Metrics;
using Chronos.Kernel.Optimization;
using Chronos.Samples.Plugins.Adapters.MT5;
using Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1303 // Do not pass literals as localized parameters – sample console app

namespace Chronos.Samples;

/// <summary>
///
/// </summary>
internal sealed class ZeroFriction : ISimulationFriction
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="type"></param>
    /// <param name="volume"></param>
    /// <param name="currentPrice"></param>
    /// <returns></returns>
    public double CalculateSlippage(string symbol, OrderType type, double volume, double currentPrice)
        => 0;
    /// <summary>
    ///
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="volume"></param>
    /// <returns></returns>
    public double CalculateCommission(string symbol, double volume)
        => 0;
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        // ---------- Configuration ----------
        const string mt5Host = "127.0.0.1";
        const int mt5Port = 5555;

        string[] symbols = { "EURUSD", "GBPUSD", "USDJPY" };
        var timeframe = TimeFrame.M5;
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        double initialBalance = 10_000;
        double leverage = 30;

        // GA settings
        int masterSeed = 42;
        int generations = 10;
        int populationSize = 10;
        double mutationRate = 0.15;
        double crossoverRate = 0.7;
        double elitismPct = 0.1;
        int tournamentSize = 3;
        int parallelism = Environment.ProcessorCount;

        // ---------- 1. Setup MT5 adapter & fetch data ----------
        var adapterConfig = new Mt5AdapterConfiguration
        {
            BridgeHost = mt5Host,
            BridgePort = mt5Port,
            HistoryCachePath = Path.Combine(AppContext.BaseDirectory, "Mt5Cache"),
            ReconnectIntervalMs = 3000,
            MaxReconnectAttempts = 10,
            TimeoutMs = 60000
        };

        Mt5Adapter? adapter = null;
        BorrowedTickData? tickData = null;

        try
        {
#pragma warning disable CA2000 // Disposed in finally block
            adapter = new Mt5Adapter(adapterConfig);
#pragma warning restore CA2000

            Console.WriteLine("Connecting to MT5...");
            if (!await adapter.ConnectAsync(CancellationToken.None).ConfigureAwait(false))
            {
                Console.WriteLine("Connection failed. Exiting.");
                return;
            }

            Console.WriteLine("Connected.");

            // Fetch historical data for all symbols
            var fetchTasks = symbols.Select(async sym =>
            {
                var req = new HistoricalDataRequest
                {
                    Symbol = sym,
                    StartTime = startDate,
                    EndTime = endDate,
                    RetentionPolicy = DataActionPolicy.PersistentCache
                };
                return await adapter.FetchHistoryToBinaryFileAsync(req, CancellationToken.None)
                    .ConfigureAwait(false);
            });
            var responses = await Task.WhenAll(fetchTasks).ConfigureAwait(false);

            // Map binary files to tick streams
            var mappedLists = new List<MemoryMappedTickList>();
            var filePaths = new List<string>();
            var streams = new IReadOnlyList<Tick>[symbols.Length];
            for (int i = 0; i < symbols.Length; i++)
            {
                var resp = responses[i];
                if (!resp.Success)
                {
                    Console.WriteLine($"Failed to fetch {symbols[i]}: {resp.ErrorMessage}");
                    return;
                }

                var mmList = BinaryDataMapper.MapTicks(resp.BinaryFilePath);
                mappedLists.Add(mmList);
                filePaths.Add(resp.BinaryFilePath);
                streams[i] = mmList;
                Console.WriteLine($"Fetched {resp.TotalRecords} ticks for {resp.Symbol}");
            }

            // Wrap in BorrowedTickData (will be disposed in finally)
#pragma warning disable CA2000 // Disposed in finally block
            tickData = new BorrowedTickData(streams, symbols, mappedLists, filePaths, adapter);
#pragma warning restore CA2000

            // ---------- 2. Build specifications ----------
            var symbolRequests = symbols
                .Select(s => new SymbolRequest(s, ImmutableArray.Create(timeframe)))
                .ToImmutableArray();

            var fitnessModel = new NetProfitFitness();

            var strategySpec = StrategySpecification.CreateValidated(initialBalance, leverage, symbolRequests, fitnessModel: fitnessModel);

            strategySpec = strategySpec with { FrictionModel = new ZeroFriction() };

            var execSpec = ExecutionSpecification.CreateValidated(
                startDate, endDate,
                warmupBars: 5,
                maxOpenPositions: 3,
                stopOutLevel: 0.5,
                latencyTicks: 0,
                geneInitializationSeed: masterSeed,
                maxParallelThreads: parallelism);

            var optSpec = OptimizationSpecification.CreateValidated(
                masterSeed, generations, populationSize,
                windows: 1,
                trainSplit: 0.8,
                mutationRate, crossoverRate,
                elitismPct, tournamentSize,
                fitnessModel);

            var symbolProperties = new Dictionary<string, SymbolProperties>
            {
                ["EURUSD"] = new()
                    { AssetClass = AssetClass.CFD, TickSize = 0.00001, ContractSize = 100_000, MinVolume = 0.01 },
                ["GBPUSD"] = new()
                    { AssetClass = AssetClass.CFD, TickSize = 0.00001, ContractSize = 100_000, MinVolume = 0.01 },
                ["USDJPY"] = new()
                    { AssetClass = AssetClass.CFD, TickSize = 0.001, ContractSize = 100_000, MinVolume = 0.01 }
            };

            // ---------- 3. Build gene schema ----------
            var neuralTopology = new[] { 5, 5, 1 };
            var schema = GeneInjector.BuildCompleteSchema(
                typeof(CfdSmtDivergenceStrategy), neuralTopology, ActivationFunction.Tanh);
            Console.WriteLine($"Total genes: {schema.Count}");

            // ---------- 4. Genetic optimiser ----------
            var optimiser = new GeneticOptimizer(
                schema,
                populationSize,
                masterSeed,
                mutationRate,
                crossoverRate,
                elitismPct,
                tournamentSize,
                stagnationGenerationsBeforeHyper: 3,
                maxDegreeOfParallelism: parallelism);

            // ---------- 5. Fitness evaluator ----------
            Func<Chromosome, CancellationToken, Task<double>> evaluator =
                async (chromosome, ct) =>
                {
                    var strategy = new CfdSmtDivergenceStrategy();
                    var calculator = new Mt5MarketCalculator();

                    var input = new BacktestInput
                    {
                        TickStreams = streams,
                        Symbols = symbols,
                        Strategy = strategy,
                        StrategySpecification = strategySpec,
                        ExecutionSpecification = execSpec,
                        MarketCalculator = calculator,
                        SymbolProperties = symbolProperties,
                        Genes = chromosome.Genes,
                        NeuralNetwork = null,
                        GeneInitializationSeed = execSpec.GeneInitializationSeed,
                        MessageBus = new MessageBus()
                    };

                    var runner = new BacktestRunner();
                    var result = await runner.RunAsync(input, ct).ConfigureAwait(false);
                    Console.WriteLine($"Trades: {result.TotalTrades}");
                    return FitnessCalculator.Calculate(result, fitnessModel, initialBalance);
                };

            // ---------- 6. Run the GA ----------
            Console.WriteLine("Initializing population...");
            optimiser.Initialize();

            for (int gen = 0; gen < generations; gen++)
            {
                Console.Write($"Gen {gen + 1}/{generations}: evaluating... ");
                await optimiser.EvaluateAsync(evaluator, CancellationToken.None).ConfigureAwait(false);
                var best = optimiser.BestSolution;
                Console.WriteLine($"Best fitness = {best.Fitness:F2} (hyper={optimiser.IsHyperMutation})");
                if (gen < generations - 1)
                    optimiser.Evolve();
            }

            // ---------- 7. Print best chromosome ----------
            var finalBest = optimiser.BestSolution;
            Console.WriteLine($"\nOptimisation complete. Best fitness: {finalBest.Fitness:F2}");
            Console.WriteLine("Best genes:");
            for (int i = 0; i < finalBest.Genes.Length; i++)
            {
                string name = i < schema.Count ? schema[i].Name : "NN_W" + (i - schema.Count);
                Console.WriteLine($"  {name}: {finalBest.Genes[i]:F4}");
            }
        }
        finally
        {
            // Cleanup – adapter.DisconnectAsync fully cleans up the bridge.
            if (tickData is not null)
                await tickData.DisposeAsync().ConfigureAwait(false);

            if (adapter is not null)
                await adapter.DisconnectAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Simple fitness model that optimises net profit.
    /// </summary>
    private sealed class NetProfitFitness : IFitnessModel
    {
        public double Evaluate(double finalBalance, double initialBalance, double maxDrawdown,
            double maxDailyDrawdown, int totalTrades, IReadOnlyList<Position> history)
        {
            if (totalTrades == 0)
                return -1_000_000;   // huge penalty for no trades
            return finalBalance - initialBalance;
        }
    }
}

```
</file>


<file path="Chronos/.editorconfig">
```
# Chronos .editorconfig – strict C# coding standards

root = true

[*]
charset = utf-8
end_of_line = crlf
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true



[*.cs]
# Diagnostic rules
dotnet_diagnostic.CS1591.severity = error        # Missing XML comment on public API
dotnet_diagnostic.IDE0044.severity = suggestion   # Make field readonly
dotnet_diagnostic.IDE0017.severity = suggestion   # Object initialiser can be simplified
dotnet_diagnostic.IDE1006.severity = error        # Naming rule violation
dotnet_diagnostic.CA1062.severity = warning       # Validate arguments of public methods
dotnet_diagnostic.CA1822.severity = suggestion    # Member does not access instance data
dotnet_diagnostic.CA1003.severity = none       # Custom event delegates are intentional
dotnet_diagnostic.CA1819.severity = none       # High-perf mutable arrays exposed deliberately
dotnet_diagnostic.CA1716.severity = none       # False positive on IIndicatorRegistry
dotnet_diagnostic.CA1032.severity = none       # Domain exceptions with required parameters
csharp_indent_labels = one_less_than_current
csharp_using_directive_placement = outside_namespace:silent
csharp_prefer_simple_using_statement = true:suggestion
csharp_prefer_braces = false:warning
csharp_style_namespace_declarations = file_scoped:silent
csharp_style_prefer_method_group_conversion = true:silent
csharp_style_prefer_top_level_statements = true:silent
csharp_style_prefer_primary_constructors = true:suggestion
csharp_prefer_system_threading_lock = true:suggestion
csharp_style_expression_bodied_methods = true:silent
csharp_style_expression_bodied_constructors = false:silent
csharp_style_expression_bodied_operators = false:silent
csharp_style_expression_bodied_properties = true:silent
csharp_style_expression_bodied_indexers = true:silent
csharp_style_expression_bodied_accessors = true:silent
csharp_style_expression_bodied_lambdas = true:silent
csharp_style_allow_blank_lines_between_consecutive_braces_experimental = true:silent

[tests/**/*.cs]
dotnet_diagnostic.CS1591.severity = none
dotnet_diagnostic.CS1707.severity = none
dotnet_diagnostic.xUnit1051.severity = none

# Style
csharp_style_var_when_type_is_apparent = true:silent
csharp_style_var_elsewhere = false:silent
csharp_style_expression_bodied_methods = true:silent
csharp_style_expression_bodied_properties = true:silent
csharp_style_expression_bodied_constructors = false:silent
csharp_prefer_braces = true:warning
csharp_style_namespace_declarations = file_scoped:silent
csharp_style_prefer_readonly_struct = true:suggestion

# Code block layout
csharp_preserve_single_line_blocks = false
csharp_preserve_single_line_statements = false

# CA1707: Identifiers should not contain underscores
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1812.severity = none

[*.{cs,vb}]
#### Naming styles ####

# Naming rules

dotnet_naming_rule.interface_should_be_begins_with_i.severity = suggestion
dotnet_naming_rule.interface_should_be_begins_with_i.symbols = interface
dotnet_naming_rule.interface_should_be_begins_with_i.style = begins_with_i

dotnet_naming_rule.types_should_be_pascal_case.severity = suggestion
dotnet_naming_rule.types_should_be_pascal_case.symbols = types
dotnet_naming_rule.types_should_be_pascal_case.style = pascal_case

dotnet_naming_rule.non_field_members_should_be_pascal_case.severity = suggestion
dotnet_naming_rule.non_field_members_should_be_pascal_case.symbols = non_field_members
dotnet_naming_rule.non_field_members_should_be_pascal_case.style = pascal_case

# Symbol specifications

dotnet_naming_symbols.interface.applicable_kinds = interface
dotnet_naming_symbols.interface.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.interface.required_modifiers = 

dotnet_naming_symbols.types.applicable_kinds = class, struct, interface, enum
dotnet_naming_symbols.types.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.types.required_modifiers = 

dotnet_naming_symbols.non_field_members.applicable_kinds = property, event, method
dotnet_naming_symbols.non_field_members.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_symbols.non_field_members.required_modifiers = 

# Naming styles

dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.required_suffix = 
dotnet_naming_style.begins_with_i.word_separator = 
dotnet_naming_style.begins_with_i.capitalization = pascal_case

dotnet_naming_style.pascal_case.required_prefix = 
dotnet_naming_style.pascal_case.required_suffix = 
dotnet_naming_style.pascal_case.word_separator = 
dotnet_naming_style.pascal_case.capitalization = pascal_case

dotnet_naming_style.pascal_case.required_prefix = 
dotnet_naming_style.pascal_case.required_suffix = 
dotnet_naming_style.pascal_case.word_separator = 
dotnet_naming_style.pascal_case.capitalization = pascal_case
dotnet_style_operator_placement_when_wrapping = beginning_of_line
tab_width = 4

```
</file>


<file path="Chronos/Directory.Build.props">
```props
<Project>
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <OptimizationPreference>Speed</OptimizationPreference>

        <AnalysisLevel>latest-all</AnalysisLevel>
        <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
        <AccelerateBuildsInVisualStudio>true</AccelerateBuildsInVisualStudio>
        <NoWarn>$(NoWarn);NU1900</NoWarn>

        <Version>0.1.0</Version>
        <Authors>Sina Valizadeh</Authors>
        <Company>Chronos</Company>
        <Copyright>© 2026 Chronos</Copyright>
    </PropertyGroup>
</Project>

```
</file>


<file path="Chronos/global.json">
```json
{
    "sdk": {
        "version": "10.0.300",
        "rollForward": "latestMajor",
        "allowPrerelease": false
    }
}

```
</file>


<file path="Chronos/docs/Chronos Plugin Developer Guide (v1.0.0 LTS).md">