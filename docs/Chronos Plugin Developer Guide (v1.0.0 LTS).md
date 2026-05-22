**Chronos Plugin Developer Guide**

**Version:** 1.0.0 LTS
**Audience:** Plugin developers (strategies, adapters)
**Status:** Authoritative
**Last Updated:** 2026-05-19

---

## 1. Introduction

Welcome to the Chronos Plugin Developer Guide. This document teaches you how to create **adapters** (to connect Chronos to exchanges or brokers) and **strategies** (the trading logic) as plug‑in assemblies. Chronos v1.0.0 LTS supports two plugin types; future versions will expand this ecosystem (indicators, risk managers, neural models, etc.).

### 1.1 What You Will Learn

- How to set up a plugin project and reference the Chronos SDK
- How to implement a fully functional adapter (historical data, live streaming, execution)
- How to implement a strategy with lifecycle methods, indicators, and optimization genes
- How to package, version, and deploy your plugin

### 1.2 Prerequisites

- .NET 10 SDK or later (matching the engine’s target framework)
- Basic knowledge of financial trading concepts (bid/ask, margin, orders)
- A text editor or IDE (Visual Studio, Rider, VS Code)

### 1.3 Where to Get Help

- The **Chronos Abstractions** NuGet package contains XML documentation for every public member.
- The `Chronos.Samples` repository provides full, working examples of adapters and strategies.
- This guide is your primary reference; the closed‑source engine internals are not documented for plugin developers.

---

## 2. Plugin Concepts

### 2.1 What is a Plugin?

A plugin is a .NET assembly (DLL) that implements one or more public interfaces defined in the `Chronos.Abstractions` NuGet package. The Chronos engine discovers and loads plugins at runtime through isolated contexts. Plugins never reference the engine directly; they only depend on the SDK.

### 2.2 Plugin Types in v1.0.0 LTS

| Type | Interface(s) | Purpose |
|------|--------------|---------|
| **Adapter** | `IAdapter` (combining `IHistoricalDataProvider`, `ILiveDataProvider`, `IExecutionProvider`) | Connects Chronos to a specific broker or exchange. Provides historical tick data, live price streaming, and order execution. |
| **Strategy** | `IStrategy` (usually via `StrategyBase`) | Contains the trading logic. Receives ticks, accesses indicators, places orders, and exposes optimizable genes. |

### 2.3 The Chronos SDK

The SDK is the `Chronos.Abstractions` NuGet package. It contains **only** contracts (interfaces, abstract classes, records, enums, and utilities) – no runtime logic, no GA engine, no broker implementations. You can freely redistribute the package; it may be open‑sourced later.

**Important:** The SDK includes `ChronosRandom` (portable RNG), `TickWindow`, `BinaryDataMapper`, etc. These are helpers for your plugin code; you are free to use them or implement your own. However, any randomness that affects trading decisions must derive from the master seed (see §8.4).

---

## 3. Project Setup

### 3.1 Create a Class Library

Create a .NET class library targeting `net10.0` (or later LTS). Example `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks> <!-- Only if using unsafe code for high‑perf I/O -->
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Chronos.Abstractions" Version="1.0.0" />
    </ItemGroup>

</Project>
```

**Note:** The `AllowUnsafeBlocks` flag is only needed if you use unsafe code; adapters that implement memory‑mapped tick files may require it.

### 3.2 Assembly Attributes

Every plugin assembly must declare the **target SDK version** using `ChronosSdkVersionAttribute`. Adapters should also declare their own release version. Example `AssemblyInfo.cs`:

```csharp
using Chronos.Abstractions.Plugins;
using Chronos.Abstractions.Adapters;

[assembly: ChronosSdkVersion("1.0.0")]
[assembly: AdapterVersion("1.2.3")]          // Adapters only
```

The engine validates these versions at load time. A major version mismatch may prevent loading (see §9).

---

## 4. Developing an Adapter

An adapter bridges Chronos and a real exchange/broker. You must implement `IAdapter`, which inherits three sub‑interfaces.

### 4.1 The `IAdapter` Interface

```csharp
public interface IAdapter : IHistoricalDataProvider, ILiveDataProvider, IExecutionProvider
{
    string AdapterName { get; }
    IMarketCalculator Calculator { get; }
    bool IsConnected { get; }
    TimeFrame[]? GetSupportedTimeframes(string symbol);
    Task<bool> ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
}
```

You must also mark your adapter class with `[AdapterName("UniqueName")]` so the engine can discover it.

### 4.2 Historical Data Provider

**Interface:** `IHistoricalDataProvider`

```csharp
Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(
    HistoricalDataRequest request, CancellationToken cancellationToken);
Task DeleteHistoryFileAsync(string filePath);
Task NotifyFileSafeToDeleteAsync(string filePath);
```

**Purpose:** Download or convert historical tick data and store it in a binary file that Chronos can memory‑map.

**Implementation steps:**

1. **Download data** from your broker’s API (e.g., REST). You can also convert candles/bars into synthetic ticks using `TickSynthesizer.BarsToTicks()`.
2. **Write to binary file** using `BinaryDataMapper.WriteTicksToBinary()`. The ticks must be sorted by ascending `Time`.
3. **Return a `HistoricalDataResponse`** with the full file path, success status, and record count.

**Binary file format:** The file starts with an 8‑byte header (`uint magic = 0x53524843`, `int version = 1`), followed by raw `Tick` structs (`[StructLayout(LayoutKind.Sequential, Pack=1)]`). The engine will open the file via `MemoryMappedTickList`; you do not need to implement reading – only writing.

**File lifecycle:** The engine calls `NotifyFileSafeToDeleteAsync()` after it has finished reading the file. Only then may you delete it, based on your retention policy. Do not delete the file before receiving that notification.

**Example:**

```csharp
public async Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(
    HistoricalDataRequest request, CancellationToken ct)
{
    // 1. Fetch bars from API (not shown)
    Bar[] bars = await FetchBarsAsync(request.Symbol, request.StartTime, request.EndTime, ct);
    // 2. Convert to ticks (4 synthetic ticks per bar)
    Tick[] ticks = TickSynthesizer.BarsToTicks(bars);
    // 3. Ensure sorted by time (already sorted by bar conversion)
    // 4. Write to binary
    string path = Path.Combine(DataDirectory, $"{request.Symbol}_{request.StartTime:yyyyMMdd}.chrs");
    BinaryDataMapper.WriteTicksToBinary(path, ticks);
    return new HistoricalDataResponse
    {
        Symbol = request.Symbol,
        Success = true,
        BinaryFilePath = path,
        TotalRecords = ticks.Length
    };
}
```

### 4.3 Live Data Provider

**Interface:** `ILiveDataProvider`

```csharp
Task SubscribeAsync(string symbol);
Task UnsubscribeAsync(string symbol);
event Action<string, Tick> OnTickReceived;
```

**Purpose:** Stream real‑time tick data directly into the engine.

**Implementation steps:**

- Connect to the broker’s WebSocket stream for the given symbol.
- For each tick update, construct a `Tick` struct and raise `OnTickReceived?.Invoke(symbol, tick)`.
- The engine calls `SubscribeAsync` once per symbol; you are responsible for maintaining a subscription list and preventing duplicates.
- `UnsubscribeAsync` should disconnect from that symbol’s stream.

**Thread safety:** The engine may call subscribe/unsubscribe from multiple threads. You must ensure your adapter is thread‑safe.

**Important:** Live ticks are **not** stored in files. The engine processes them in‑memory immediately.

### 4.4 Execution Provider

**Interface:** `IExecutionProvider`

```csharp
Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest request);
Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl, double? tp, double? price);
Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double? volume);
Task<AdapterOrderResponse> CancelAsync(long ticket);
Task<(double Balance, double Equity)> GetAccountInfoAsync(CancellationToken ct);
Task<IReadOnlyList<Position>> GetActivePositionsAsync();
Task<IReadOnlyList<Order>> GetPendingOrdersAsync();
Task<SymbolProperties?> GetSymbolPropertiesAsync(string symbol, CancellationToken ct);
event Action<ExecutionReport> OnExecutionUpdate;
```

**Purpose:** Send orders, manage positions, and receive execution updates.

**Key methods:**

- `ExecuteOrderAsync`: Translates an `AdapterOrderRequest` into a broker‑specific order. Returns immediately with a ticket; subsequent fills come through `OnExecutionUpdate`.
- `ModifyOrderAsync`, `CancelAsync`, `ClosePositionAsync`: delegate to the broker API.
- `GetAccountInfoAsync`: returns current balance and equity.
- `GetActivePositionsAsync`, `GetPendingOrdersAsync`: return broker‑side lists.
- `GetSymbolPropertiesAsync`: returns exchange‑specific metadata (`SymbolProperties` record). This is crucial – it tells Chronos how to calculate margins, tick sizes, swap rates, etc. You must fill all fields accurately.

**The `OnExecutionUpdate` event:** Raise this whenever an order fills, partially fills, cancels, etc. The engine maintains its own state from these reports. The event handler signature is `Action<ExecutionReport>`; ensure you raise it asynchronously and catch exceptions internally (the engine will log them, but your adapter should not crash).

**Thread‑safety:** The engine may call multiple execution methods concurrently (e.g., while a tick is being processed). Use locks where necessary.

### 4.5 Market Calculator

The `Calculator` property of `IAdapter` returns an instance of `IMarketCalculator`. You must implement this interface with exchange‑specific math.

```csharp
public interface IMarketCalculator
{
    double NormalizeVolume(SymbolProperties props, double requestedVolume);
    double NormalizePrice(SymbolProperties props, double requestedPrice);
    double CalculateRequiredMargin(SymbolProperties props, double price, double volume, double leverage);
    double CalculatePnL(SymbolProperties props, double entryPrice, double currentPrice, double volume, OrderType type);
    double CalculateCommission(SymbolProperties props, double price, double volume);
    double CalculateSwap(SymbolProperties props, double volume, OrderType type, long openTime, long closeTime);
    double CalculateFunding(SymbolProperties props, double volume, double openPrice, OrderType type, long currentTime, long lastFundingTime);
    bool IsPendingOrderTriggered(SymbolProperties props, OrderType pendingType, double bid, double ask, double orderPrice);
    double CalculateHoldingCost(SymbolProperties props, double volume, double openPrice, OrderType type, long fromTime, long toTime);
}
```

**How it’s used:** Both the simulated and live broker call these methods to maintain consistent accounting. The calculator must be **stateless** (pure functions) and **deterministic**.

**Example margin calculation (crypto perpetual):**
```csharp
public double CalculateRequiredMargin(SymbolProperties props, double price, double volume, double leverage)
    => (price * volume * props.ContractSize) / leverage;
```

**Important:** All price/volume normalization must round to the exchange’s tick size. Failure to do so will cause rejections and parity mismatches.

### 4.6 Connection Lifecycle

Implement `ConnectAsync` and `DisconnectAsync` to manage the underlying transport (WebSocket, REST session). Return `true` from `ConnectAsync` if the connection succeeded. The engine calls `ConnectAsync` once on startup, then uses the provider methods.

### 4.7 Example Adapter Skeleton

```csharp
[AdapterName("MyExchange")]
public class MyExchangeAdapter : IAdapter
{
    public string AdapterName => "MyExchange";
    public IMarketCalculator Calculator { get; }
    public bool IsConnected { get; private set; }

    public MyExchangeAdapter()
    {
        Calculator = new MyExchangeCalculator();
    }

    public Task<bool> ConnectAsync(CancellationToken ct) { /* ... */ }
    public Task DisconnectAsync() { /* ... */ }

    // ... implement all other interface members ...
}
```

### 4.8 Testing Your Adapter

To test locally, you need a copy of the Chronos Engine (the same binary used in production). With a free development license, you can run the engine in “mock live” mode or run backtests. Place your adapter DLL in the plugin folder configured for the engine, and it will be loaded automatically. See the Installation & Deployment Guide for engine setup.

**Important:** MetaTrader 5 adapters cannot run on Linux (MT5 provides only Windows DLLs). Document this limitation for your users.

---

## 5. Developing a Strategy

Strategies contain the decision logic. They react to ticks, use indicators, place orders, and can be optimized via genetic algorithms.

### 5.1 The `IStrategy` Interface

```csharp
public interface IStrategy
{
    Task OnConfigureAsync(StrategySpecification spec);
    Task OnStartAsync(IIndicatorRegistry indicators);
    Task OnTickAsync(Tick tick);
    Task OnStopAsync();
    void InjectGenes(double[] genes);
}
```

A simpler approach is to derive from `StrategyBase`, which provides convenience members.

### 5.2 Strategy Lifecycle

1. `OnConfigureAsync` – Called once before data starts. The `spec` parameter (immutable) contains initial balance, leverage, requested symbols/timeframes, and optional friction/fitness models. Store what you need.
2. `OnStartAsync` – Called just before the first tick. The indicator registry is ready. Use this to pre‑allocate buffers, initialize indicators, etc.
3. `OnTickAsync` – Called sequentially for every tick, in chronological order. This is your main logic entry point.
4. `OnStopAsync` – Called after the last tick (backtest) or when a live session is stopped. Clean up resources.
5. `InjectGenes` – Called by the optimizer to load a chromosome’s gene array. You override this to apply the values to your properties/neural network.

### 5.3 Using `StrategyBase`

`StrategyBase` provides:

- `Broker` – The trading interface (simulated in backtest, live adapter in production).
- `TickWindow` – Access to recent ticks and OHLC calculations (never a bar‑based callback).
- `Indicators` – The registry for your indicators.
- `Spec` – The immutable configuration.
- `PrimarySymbol` – Shorthand for the first requested symbol.
- `NeuralNet` – Your optional neural network instance.
- Helper methods: `BuyAsync`, `SellAsync`, `ModifyOrderAsync`, `CloseAllAsync`, etc.

Override the virtual lifecycle methods and add your logic.

### 5.4 Example Strategy Skeleton

```csharp
public class SimpleMaStrategy : StrategyBase
{
    // Optimizable genes
    [Gene(10, 200, Step = 1)]
    public int FastPeriod { get; set; } = 50;

    [Gene(50, 500, Step = 1)]
    public int SlowPeriod { get; set; } = 200;

    private SmaIndicator _fastSma;
    private SmaIndicator _slowSma;

    public override async Task OnConfigureAsync(StrategySpecification spec)
    {
        await base.OnConfigureAsync(spec);
        // any additional setup
    }

    public override async Task OnStartAsync(IIndicatorRegistry indicators)
    {
        await base.OnStartAsync(indicators);
        _fastSma = indicators.Get<SmaIndicator>(PrimarySymbol, TimeFrame.M1, FastPeriod);
        _slowSma = indicators.Get<SmaIndicator>(PrimarySymbol, TimeFrame.M1, SlowPeriod);
    }

    public override async Task OnTickAsync(Tick tick)
    {
        // Access indicator values: _fastSma[0], _slowSma[0]
        // Place orders via Broker.ExecuteMarketOrderAsync(...)
    }
}
```

### 5.5 Indicators

Indicators are created via `IIndicatorRegistry.Get<T>(args)`. The registry caches them; use the same arguments to retrieve the same instance. Your strategy receives the registry in `OnStartAsync`.

The base `Indicator` class manages a circular buffer. Override `Initialize` and `Calculate` to implement your own. For example, `SmaIndicator` updates on each tick using a lookback. Use the `TickWindow` for OHLC data if your indicator needs it.

### 5.6 Using TickWindow for OHLC Data

Chronos is tick‑only, but you can still get OHLC aggregates:

```csharp
// Get OHLC for the last completed 1‑minute window
TickWindow.GetCurrentStats(PrimarySymbol, TimeFrame.M1, PriceType.Bid,
    out double open, out double high, out double low, out double close,
    out double volume, out bool isComplete);
if (isComplete) { /* use OHLC */ }
```

`TickWindow` also provides `WindowCompleted` events if you override `OnWindowCompletedAsync` in `StrategyBase`.

**Never call `DateTime.UtcNow` inside a strategy** – it will break determinism and is prohibited by the engine. Use the tick’s time (`tick.Time`) or `TickClock` (available via the broker if needed) for all time‑based decisions.

### 5.7 Gene‑Based Optimization

To make your strategy optimizable, mark properties with `[Gene]`. The GA will automatically discover them via reflection.

```csharp
[Gene(0.1, 5.0, Step = 0.1)]
public double RiskPercent { get; set; } = 1.0;
```

- `Min`, `Max` – the range.
- `Step` – discrete step size. 0 means continuous.
- `Type` – `Continuous`, `Discrete`, etc.
- `Order` – sets the gene order in the chromosome.

During optimization, the engine calls `InjectGenes(double[] genes)`, which maps the array to your properties (clamped to ranges). `StrategyBase` already implements this using `GeneInjector`. If you override `InjectGenes`, call `base.InjectGenes(genes)`.

### 5.8 Neural Networks

If your strategy uses a neural network, instantiate a `FeedForwardNetwork` in your constructor with a topology (e.g., `new [] {5, 10, 1}`). Assign it to `NeuralNet`. The gene injector will append its weights to the chromosome.

Implement `INeuralNetwork` yourself only if you need a custom network type; otherwise use the built‑in `FeedForwardNetwork`. In `OnTickAsync`, call `NeuralNet.FeedForward(inputs)` and use the output for signals.

### 5.9 Friction and Fitness Models

- `ISimulationFriction` – If your strategy needs custom slippage/commission in backtesting, implement this interface and pass it in the `StrategySpecification`. Otherwise, the default friction model from the adapter is used.
- `IFitnessModel` – Required for optimization. Implement a function that scores a backtest result (higher = better). Example: `return netProfit - 2 * maxDrawdown;`

---

## 6. Configuration and Symbol Properties

### 6.1 Strategy Specification

The `StrategySpecification` record is immutable and passed by the engine. It contains:
- `InitialBalance`
- `Leverage`
- `RequestedSymbols` – list of `SymbolRequest` (symbol + timeframes)
- `FrictionModel` (optional)
- `FitnessModel` (optional)

Your strategy never instantiates this; the Cloud provides it.

### 6.2 Symbol Properties

`SymbolProperties` is an exchange‑specific record filled by your adapter’s `GetSymbolPropertiesAsync`. It defines:
- Tick size, contract size, margin rates, swap/funding parameters, etc.

The engine uses this data in the broker’s calculations. Ensure all fields are accurate for the target exchange.

---

## 7. Testing Strategies Locally

- Run the Chronos Engine locally (development license). It will load your strategy DLL.
- Use the Cloud interface (or a local test tool) to configure a backtest with your strategy.
- For live testing, the engine’s development license supports a mock adapter that simulates order execution without real money.

---

## 8. Best Practices

### 8.1 Performance

- `OnTickAsync` is called millions of times in a backtest. **Avoid allocations** inside this method.
- Pre‑allocate arrays, use `Span<T>` if possible, and cache indicator references.
- Do not use `async`/`await` inside `OnTickAsync` if your logic is CPU‑bound; keep it synchronous. (The broker’s orders return immediately in simulation.)

### 8.2 Thread Safety for Adapters

- The engine may call your adapter methods from multiple threads simultaneously (e.g., a tick event and a periodic state sync). Use `SemaphoreSlim` or `lock` to protect shared state.
- The `OnExecutionUpdate` event handler can be invoked on any thread; ensure your event raising code is thread‑safe.

### 8.3 Determinism for Strategies

- Do not use `System.Random` unless it’s seeded deterministically and only for non‑trading purposes (e.g., logging).
- Use `ChronosRandom` if you need a PRNG; seed it from the master seed passed through configuration or genes.
- Do not access `DateTime.UtcNow` or system clocks in trading logic.

### 8.4 Avoiding Hard‑coded Market Logic

Your strategy should work across multiple asset classes if it does not depend on specific tick sizes or contract details. Let the broker handle those through `SymbolProperties`.

---

## 9. Versioning and Compatibility

### 9.1 Declaring Compatibility

Your plugin assembly must include `[assembly: ChronosSdkVersion("1.0.0")]`. The engine’s version manager checks this attribute.

- Plugins targeting an older major version (e.g., 0.9.x) may be loaded if backward‑compatible.
- Plugins targeting a newer major version (e.g., 2.0.0) are rejected unless a compatibility mode is configured.

### 9.2 Adapter Versioning

Use `[assembly: AdapterVersion("1.0.0")]` for diagnostics. This version is displayed in Chronos Cloud.

### 9.3 Breaking Changes

Chronos follows SemVer. Minor releases add new interfaces/properties without breaking existing plugins. Major releases may break APIs, but older plugins can still run if they target an older SDK version (provided the engine supports that SDK major).

---

## 10. Packaging and Deployment

### 10.1 Build Output

Compile your plugin as a release DLL. Strong‑name signing is recommended (required in production). For .NET, add `<StrongNameKeyFile>mykey.snk</StrongNameKeyFile>` to your project.

### 10.2 Uploading to Chronos Cloud

For end‑users, you upload the DLL to Chronos Cloud through the web interface. The Cloud verifies the SDK version and stores it. When a user deploys your adapter/strategy, the Cloud pushes the signed DLL to their engine.

### 10.3 Local Testing

During development, place the DLL in the engine’s designated `plugins/` folder (create if missing). The engine will scan and load it automatically.

---

## 11. Platform‑Specific Notes

- **MetaTrader 5 Adapters:** MetaTrader 5 provides Windows‑only DLLs, so an MT5 adapter cannot run on Linux servers. If you develop an adapter for MT5, document this restriction clearly.
- **Other Brokers:** Most modern APIs (REST + WebSocket) are cross‑platform. Test your adapter on both Windows and Linux if you intend to support both.

---

## 12. Further Resources

- **Chronos.Abstractions** NuGet package: `https://nuget.org/packages/Chronos.Abstractions` (once published)
- **Sample Plugins**: See the `Chronos.Samples` repository for complete working examples.
- **Chronos Principles**: For a high‑level understanding of the engine’s design rules, read `ChronosPrinciples.md`.

---

*This guide will be updated with each SDK release.*
