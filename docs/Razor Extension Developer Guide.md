# Razor Extension Developer Guide

**Version:** 1.0.0 LTS  
**Audience:** Extension developers (adapters, strategies, indicators, hook plugins, NN models)  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

## 1. Introduction

This guide teaches you how to create extensions for Razor. An extension is a .NET DLL that implements one or more public contracts from the `Razor.Core.Sdk` NuGet package. The engine discovers and loads extensions at runtime through isolated contexts, and activation is managed through Razor Cloud.

In v1.0.0 LTS, the following extension types are supported:

- **Adapter** – Connects Razor to a broker or exchange (`IAdapterCapability`).
- **Strategy** – Trading logic that reacts to ticks and places orders (`IStrategyCapability`).
- **Indicator** – Technical analysis tools (SMA, RSI, etc.) (`Indicator` base class).
- **Hook Plugin** – Intercepts engine events via filter and action hooks (`IHookManifest`).
- **Neural Network Model** – Feed‑forward, ONNX, LSTM, RL models for strategy use (`INeuralNetworkModel`).

A single DLL can combine any of these — for example, a strategy that also registers hooks. All extension types share the same versioning and deployment mechanisms.

### 1.1 Prerequisites

- .NET 10 SDK or later.
- Basic knowledge of trading concepts (bid/ask, margin, orders).
- A text editor or IDE (Visual Studio, Rider, VS Code).

### 1.2 Where to Get Help

- The `Razor.Core.Sdk` NuGet package contains XML documentation for every public member.
- This guide is your primary reference.

---

## 2. Extension Concepts

### 2.1 Architecture Overview

Razor extensions are organized into three concepts:

| Concept | What It Does | Directory |
|---------|-------------|-----------|
| **Slots** | Required capabilities (Adapter, Strategy, NN Model) | `Adapters/`, `Strategies/`, `NeuralNetworks/` |
| **Indicators** | Technical analysis computations | `Indicators/` |
| **Hooks** | Intercept and observe engine events | `Plugins/` (any scanned directory) |

**Slots** provide core functionality the engine needs to operate. The Cloud activates exactly one Adapter and one Strategy per engine instance, and optionally one NN Model if the strategy requires it.

**Hooks** are the primary extensibility mechanism. A hook plugin implements `IHookManifest` and registers callbacks on named hook points with priorities. Hook plugins are always active once loaded—they run whenever their registered hooks fire.

### 2.2 The Razor SDK

The SDK is the `Razor.Core.Sdk` NuGet package. It contains **only** contracts (interfaces, abstract classes, records, enums, and utilities) – no runtime logic, no GA engine, no broker implementations. You can freely redistribute the package.

The SDK includes helper types like `CustomizedRandom` (portable RNG), `TickWindow`, `TickSynthesizer`, and `GeneInjector`. These are helpers for your extension code; you are free to use them or implement your own. However, any randomness that affects trading decisions must derive from the master seed (see §8.4).

---

## 3. Project Setup

### 3.1 Create a Class Library

Create a .NET class library targeting `net10.0`. Example `.csproj`:

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
        <PackageReference Include="Razor.Core.Sdk" Version="1.0.0" />
    </ItemGroup>

</Project>
```

**Note:** The `AllowUnsafeBlocks` flag is only needed if you use unsafe code; adapters that implement memory‑mapped tick files may require it.

### 3.2 Assembly Attributes

Every extension assembly must declare the **target SDK version** using `SdkVersionAttribute`. Example `AssemblyInfo.cs` or in your `csproj`:

```csharp
using Razor.Core.Sdk.Shared;

[assembly: SdkVersion("1.0.0")]
```

The engine validates this version at load time. A major version mismatch will prevent loading (see §10).

---

## 4. Developing an Adapter

An adapter bridges Razor and a real exchange/broker. You must implement `IAdapterCapability`, which consolidates all adapter functionality into a single interface with capability flags.

### 4.1 The `IAdapterCapability` Interface

The interface is in `Razor.Core.Sdk.Slots.Adapter`.

```csharp
public interface IAdapterCapability
{
    // Identity
    string Name { get; }
    IMarketCalculator Calculator { get; }
    bool IsConnected { get; }

    // Capability flags
    bool SupportsHistoricalData { get; }
    bool SupportsLiveData { get; }
    bool SupportsExecution { get; }

    // Connection
    Task<bool> ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();

    // Historical data
    Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(HistoricalDataRequest request, CancellationToken ct);
    Task DeleteHistoryFileAsync(string filePath);
    Task NotifyFileSafeToDeleteAsync(string filePath);

    // Live data
    Task SubscribeAsync(string symbol);
    Task UnsubscribeAsync(string symbol);
    event Action<string, Tick> OnTickReceived;

    // Execution
    Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest request);
    Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null);
    Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double? volume = null);
    Task<AdapterOrderResponse> CancelAsync(long ticket);
    Task<(double Balance, double Equity)> GetAccountInfoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Position>> GetActivePositionsAsync();
    Task<IReadOnlyList<Order>> GetPendingOrdersAsync();
    Task<SymbolProperties?> GetSymbolPropertiesAsync(string symbol, CancellationToken ct = default);
    event Action<ExecutionReport> OnExecutionUpdate;

    // Symbol support
    TimeFrame[]? GetSupportedTimeframes(string symbol);
}
```

### 4.2 Capability Flags

An adapter must declare which sub‑capabilities it supports. A data‑only adapter (no execution) sets:

```csharp
public bool SupportsHistoricalData => true;
public bool SupportsLiveData => true;
public bool SupportsExecution => false;
```

The engine queries these flags at startup and only invokes methods for supported capabilities. If an unsupported method is called, throw `NotSupportedException`.

### 4.3 Historical Data Provider

The adapter's `FetchHistoryToBinaryFileAsync` method downloads or converts historical tick data and stores it in a binary file that Razor can memory‑map.

**Implementation steps:**

1. **Download data** from your broker's API (e.g., REST). You can also convert candles/bars into synthetic ticks using `TickSynthesizer.BarsToTicks()`.
2. **Write to binary file** using `BinaryDataMapper.WriteTicksToBinary()`. The ticks must be sorted by ascending `Time`.
3. **Return a `HistoricalDataResponse`** with the full file path, success status, and record count.

**Binary file format:** The file starts with an 8‑byte header (`uint magic = 0x53524843`, `int version = 1`), followed by raw `Tick` structs (`[StructLayout(LayoutKind.Sequential, Pack=1)]`). The engine opens the file via `MemoryMappedTickList`; you do not need to implement reading – only writing.

**File lifecycle:** The engine calls `NotifyFileSafeToDeleteAsync()` after it has finished reading the file. Only then may you delete it, based on your retention policy. Do not delete the file before receiving that notification.

**Example:**

```csharp
public async Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(
    HistoricalDataRequest request, CancellationToken ct)
{
    Bar[] bars = await FetchBarsAsync(request.Symbol, request.StartTime, request.EndTime, ct);
    Tick[] ticks = TickSynthesizer.BarsToTicks(bars);
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

### 4.4 Live Data Provider

Stream real‑time tick data directly into the engine.

**Implementation steps:**

- Connect to the broker's WebSocket stream for the given symbol.
- For each tick update, construct a `Tick` struct and raise `OnTickReceived?.Invoke(symbol, tick)`.
- The engine calls `SubscribeAsync` once per symbol; maintain a subscription list and prevent duplicates.
- `UnsubscribeAsync` should disconnect from that symbol's stream.

**Thread safety:** The engine may call subscribe/unsubscribe from multiple threads. Ensure your adapter is thread‑safe.

### 4.5 Execution Provider

**Key methods:**

- `ExecuteOrderAsync`: Translates an `AdapterOrderRequest` into a broker‑specific order. Returns immediately with a ticket; subsequent fills come through `OnExecutionUpdate`.
- `ModifyOrderAsync`, `CancelAsync`, `ClosePositionAsync`: delegate to the broker API.
- `GetAccountInfoAsync`: returns current balance and equity.
- `GetActivePositionsAsync`, `GetPendingOrdersAsync`: return broker‑side lists.
- `GetSymbolPropertiesAsync`: returns exchange‑specific metadata (`SymbolProperties` record). This is crucial – it tells Razor how to calculate margins, tick sizes, swap rates, etc. You must fill all fields accurately.

**The `OnExecutionUpdate` event:** Raise this whenever an order fills, partially fills, cancels, etc. The engine maintains its own state from these reports. The event handler signature is `Action<ExecutionReport>`; ensure you raise it asynchronously and catch exceptions internally (the engine will log them, but your adapter should not crash).

**Thread‑safety:** The engine may call multiple execution methods concurrently (e.g., while a tick is being processed). Use locks where necessary.

### 4.6 Market Calculator

The `Calculator` property returns an instance of `IMarketCalculator` (in `Razor.Core.Sdk.Shared`). You must implement this interface with exchange‑specific math.

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
    double CalculateSlippage(SymbolProperties props, OrderType type, double volume, double price);
}
```

**How it's used:** Both the simulated and live broker call these methods to maintain consistent accounting. The calculator must be **stateless** (pure functions) and **deterministic**.

**Example margin calculation (crypto perpetual):**
```csharp
public double CalculateRequiredMargin(SymbolProperties props, double price, double volume, double leverage)
    => (price * volume * props.ContractSize) / leverage * props.InitialMarginRate;
```

**Important:** All price/volume normalization must round to the exchange's tick size. Failure to do so will cause rejections and parity mismatches.

Slippage and commission are now adapter‑internal. The `IMarketCalculator` provides `CalculateCommission` and `CalculateSlippage`; slippage is handled by the adapter's order execution logic. The engine no longer uses a separate `ISimulationFriction` — the adapter owns all friction.

### 4.7 Connection Lifecycle

Implement `ConnectAsync` and `DisconnectAsync` to manage the underlying transport (WebSocket, REST session). Return `true` from `ConnectAsync` if the connection succeeded. The engine calls `ConnectAsync` once on startup, then uses the provider methods.

### 4.8 Example Adapter Skeleton

```csharp
using Razor.Core.Sdk.Slots.Adapter;
using Razor.Core.Sdk.Shared;

[AdapterName("MyExchange")]
public class MyExchangeAdapter : IAdapterCapability
{
    public string Name => "MyExchange";
    public IMarketCalculator Calculator { get; }
    public bool IsConnected { get; private set; }

    public bool SupportsHistoricalData => true;
    public bool SupportsLiveData => true;
    public bool SupportsExecution => true;

    public MyExchangeAdapter()
    {
        Calculator = new MyExchangeCalculator();
    }

    public Task<bool> ConnectAsync(CancellationToken ct) { /* ... */ }
    public Task DisconnectAsync() { /* ... */ }

    // ... implement all other interface members ...
}
```

### 4.9 Testing Your Adapter

To test locally, place your adapter DLL in the `Adapters/` directory of the Razor Engine. With a free development license, you can run the engine in "mock live" mode or run backtests. See the Installation & Deployment Guide for engine setup.

**Important:** MetaTrader 5 adapters cannot run on Linux (MT5 provides only Windows DLLs). Document this limitation for your users.

---

## 5. Developing a Strategy

Strategies contain the decision logic. They react to ticks, use indicators, place orders, and can be optimized via genetic algorithms.

### 5.1 The `IStrategyCapability` Interface

The interface is in `Razor.Core.Sdk.Slots.Strategy`.

```csharp
public interface IStrategyCapability
{
    // Lifecycle
    Task OnConfigureAsync(StrategySpecification spec);
    Task OnStartAsync(IIndicatorRegistry indicators);
    void OnTick(string symbol, Tick tick);
    Task OnStopAsync();

    // Gene support
    int TotalGeneCount { get; }
    void InjectGenes(double[] genes);
    double[] ExportGenes();

    // Neural network
    bool RequiresNeuralNetwork { get; }
    INeuralNetworkModel? NeuralNetwork { get; set; }
}
```

The `symbol` parameter tells your strategy which instrument the tick belongs to. This is essential for multi‑symbol strategies. The `Tick` struct itself carries only price data (bid, ask, volume, timestamp) to maintain its fixed‑size binary layout for memory‑mapped I/O.

A simpler approach is to derive from `StrategyBase`, which provides convenience members.

### 5.2 Strategy Lifecycle

1. `OnConfigureAsync` – Called once before data starts. The `spec` parameter (immutable) contains initial balance, leverage, and requested symbols/timeframes. Store what you need.
2. `OnStartAsync` – Called just before the first tick. The indicator registry is ready. Use this to pre‑allocate buffers, initialize indicators, etc.
3. `OnTick(string symbol, Tick tick)` – Called sequentially for every tick, in chronological order. **Must be purely synchronous** to guarantee determinism in backtesting and live. The `symbol` parameter identifies which instrument the tick belongs to. This is your main logic entry point.
4. `OnStopAsync` – Called after the last tick (backtest) or when a live session is stopped. Clean up resources.
5. `InjectGenes` – Called by the optimizer to load a chromosome's gene array. You override this to apply the values to your properties/neural network.

### 5.3 Using `StrategyBase`

`StrategyBase` provides:

- `Broker` – The trading interface (simulated in backtest, live adapter in production).
- `TickWindow` – Access to recent ticks and OHLC calculations (never a bar‑based callback). **Not thread‑safe** – use only from within `OnTick` or under external synchronisation.
- `Indicators` – The registry for your indicators.
- `Spec` – The immutable configuration.
- `PrimarySymbol` – Shorthand for the first requested symbol.
- `NeuralNetwork` – Your optional `INeuralNetworkModel` instance, set by the engine.
- `TotalGeneCount` – Computed automatically from property genes + NN parameter count.
- Helper methods: `BuyAsync`, `SellAsync`, `ModifyOrderAsync`, `CloseAllAsync`, etc.
- `GeneLock` – A `ReaderWriterLockSlim` to protect gene injection while processing ticks.

Override the virtual lifecycle methods and add your logic.

### 5.4 Example Strategy Skeleton

```csharp
using Razor.Core.Sdk.Shared;

public class SimpleMaStrategy : StrategyBase
{
    [Gene(10, 200, Step = 1, Type = GeneType.Discrete)]
    public int FastPeriod { get; set; } = 50;

    [Gene(50, 500, Step = 1, Type = GeneType.Discrete)]
    public int SlowPeriod { get; set; } = 200;

    private Indicator _fastSma = null!;
    private Indicator _slowSma = null!;

    public override async Task OnStartAsync(IIndicatorRegistry indicators)
    {
        await base.OnStartAsync(indicators);
        _fastSma = indicators.Get<SmaIndicator>(PrimarySymbol, TimeFrame.M1, FastPeriod);
        _slowSma = indicators.Get<SmaIndicator>(PrimarySymbol, TimeFrame.M1, SlowPeriod);
    }

    public override void OnTick(string symbol, Tick tick)
    {
        double fast = _fastSma[0];
        double slow = _slowSma[0];

        if (fast > slow && !Broker.HasOpenPositionAsync(symbol).Result)
            _ = BuyAsync(symbol, 0.1);
        else if (fast < slow)
            _ = CloseAllAsync(symbol);
    }
}
```

### 5.5 Indicators

Indicators are created via `IIndicatorRegistry.Get<T>(args)`. The registry caches them; use the same arguments to retrieve the same instance. Your strategy receives the registry in `OnStartAsync`.

The base `Indicator` class manages a circular buffer. Override `Initialize` and `Calculate` to implement your own. For example, `SmaIndicator` updates on each tick using a lookback. Use the `TickWindow` for OHLC data if your indicator needs it.

### 5.6 Using TickWindow for OHLC Data

Razor is tick‑only, but you can still get OHLC aggregates:

```csharp
TickWindow.GetCurrentStats(symbol, TimeFrame.M1, PriceType.Bid,
    out double open, out double high, out double low, out double close,
    out double volume, out bool isComplete);
if (isComplete) { /* use OHLC */ }
```

`TickWindow` also provides `WindowCompleted` events if you override `OnWindowCompletedAsync` in `StrategyBase`.

**Thread safety:** `TickWindow` is not thread‑safe. It is designed to be called exclusively from the single‑threaded tick processing pipeline (`OnTick`). If you need to access it from a background task, acquire the strategy's `GeneLock` or another synchronisation primitive first.

**Never call `DateTime.UtcNow` inside a strategy** – it will break determinism and is prohibited by the engine. Use the tick's time (`tick.Time`) for all time‑based decisions.

### 5.7 Gene‑Based Optimization

To make your strategy optimizable, mark properties with `[Gene]`. The GA will automatically discover them via reflection.

```csharp
[Gene(0.1, 5.0, Step = 0.1, Type = GeneType.Discrete)]
public double RiskPercent { get; set; } = 1.0;
```

- `Min`, `Max` – the range.
- `Step` – discrete step size. **Only valid for `Discrete` and `Categorical` gene types.** For `Continuous`, `Structural`, and `Parametric` types, `Step` must be `0` (the default). Providing a non‑zero step for unsupported types throws an `ArgumentException`.
- `Type` – `Continuous`, `Discrete`, `Categorical`, `Structural`, `Parametric`.
- `Order` – sets the gene order in the chromosome.

During optimization, the engine calls `InjectGenes(double[] genes)`, which maps the array to your properties (clamped to ranges). `StrategyBase` already implements this using `GeneInjector`. If you override `InjectGenes`, call `base.InjectGenes(genes)`.

### 5.8 Neural Networks

If your strategy uses a neural network, set `RequiresNeuralNetwork = true` in your strategy. The engine will activate an `INeuralNetworkModel` from the `NeuralNetworks/` directory and assign it to `NeuralNetwork` before `OnStartAsync`.

The `GeneInjector` will automatically include the model's `ParameterCount` in `TotalGeneCount` and inject the neural network genes during `InjectGenes`. You can call `NeuralNetwork.Predict(inputs)` inside `OnTick` to get predictions.

You may also implement `INeuralNetworkModel` yourself to create custom architectures, ONNX wrappers, or RL models. See §7.

---

## 6. Developing a Hook Plugin

Hook plugins are the primary extensibility mechanism. Instead of implementing a typed interface, you implement `IHookManifest` and register callbacks on named hook points.

### 6.1 The `IHookManifest` Interface

```csharp
public interface IHookManifest
{
    void RegisterHooks(IHookRegistry registry);
}
```

The engine calls `RegisterHooks` once at plugin load time, passing the root `IHookRegistry`.

### 6.2 The `IHookRegistry` Interface

```csharp
public interface IHookRegistry
{
    IBacktestHooks Backtest { get; }
    ILiveHooks Live { get; }
    IOptimizationHooks Optimization { get; }
    IReportHooks Report { get; }
}
```

Each sub‑registry exposes typed hook registration points.

### 6.3 Filter Hooks

A **filter hook** transforms or rejects data flowing through the pipeline. Use `IFilterRegistration<T>.Register(Func<T, IHookContext, FilterResult<T>> callback, int priority)`.

To allow data to pass through (possibly modified):
```csharp
return FilterResult.Allow(myData);
```

To reject data with a reason:
```csharp
return FilterResult.Reject<T>("Reason for rejection");
```

### 6.4 Action Hooks

An **action hook** observes events without modifying data. Use `IActionRegistration<T>.Register(Action<T, IHookContext> callback, int priority)` for typed events, or `IActionRegistration.Register(Action<IHookContext> callback, int priority)` for parameterless events.

> **⚠ Critical:** Action hook callbacks **must be synchronous**. Do **not** use `async void` — exceptions thrown inside an `async void` delegate cannot be caught by the engine and will crash the process. If you need to perform asynchronous work (e.g., HTTP calls to an external API), queue the work externally with its own exception handling. See the dedicated subsection below for safe patterns.

---

### Safe Fire‑and‑Forget Async Patterns

Because action hook callbacks **must be synchronous**, you cannot use `async`/`await` directly. However, you may need to perform asynchronous operations (e.g., sending an HTTP request to a webhook, writing to a remote database, or sending an email) without blocking the engine's tick processing pipeline.

The safe pattern is to **fire‑and‑forget** using `Task.Run` with explicit exception handling. **Never** use `async void` – unhandled exceptions in `async void` methods will crash the engine process.

**Correct pattern (copy‑paste ready):**

```csharp
public class TelegramNotifier : IHookManifest
{
    public void RegisterHooks(IHookRegistry registry)
    {
        registry.Backtest.OnCompleted.Register(ctx =>
        {
            // Fire and forget – do NOT use async void directly.
            Task.Run(async () =>
            {
                try
                {
                    await SendTelegramNotificationAsync("Backtest completed.");
                }
                catch (Exception ex)
                {
                    // Log the error; the engine will not catch it for you.
                    // Use your preferred logging mechanism.
                    Console.Error.WriteLine($"[TelegramNotifier] Failed: {ex.Message}");
                }
            });
        });
    }

    private async Task SendTelegramNotificationAsync(string message)
    {
        // Your HTTP call here...
        await Task.CompletedTask;
    }
}
```

**Why this works:**

- `Task.Run` schedules the async delegate on the thread pool, freeing the hook callback to return immediately.
- The `try/catch` inside the delegate ensures that any exception is logged and does not propagate to the CLR.
- The engine's tick processing is not blocked, preserving determinism and performance.

**Alternative pattern (using `ContinueWith`):**

```csharp
Task.Run(() => SendTelegramNotificationAsync("Backtest completed."))
    .ContinueWith(t =>
    {
        if (t.IsFaulted && t.Exception != null)
        {
            Console.Error.WriteLine($"[TelegramNotifier] Failed: {t.Exception.Message}");
        }
    }, TaskContinuationOptions.OnlyOnFaulted);
```

Both patterns are acceptable. The first is more readable and recommended.

**Important:** Always ensure that your background tasks do not hold references to engine objects that might be disposed (e.g., `IBroker`, `TickWindow`). If you need to capture such objects, do so only during the hook callback and do not keep them alive beyond the callback's scope.

---

### 6.5 Priorities

Each hook registration accepts a `priority` parameter (default 100). Lower numbers execute earlier. Hooks with the same priority are ordered alphabetically by plugin name, then by registration order. This guarantees deterministic execution.

### 6.6 Complete Hook Catalog

#### Backtest Hooks (via `registry.Backtest`)

| Hook | Type | Description |
|------|------|-------------|
| `OnStart` | Action | Fires when a backtest starts. |
| `OnTickReceived` | Filter\<Tick\> | Filter a tick as it is received from the tick stream. |
| `OnTickStrategyBefore` | Filter\<Tick\> | Filter the tick before it is passed to the strategy. |
| `OnTickStrategyAfter` | Action\<Tick\> | Action after the strategy has processed a tick. |
| `OnTickCompleted` | Action\<Tick\> | Action after all processing for a tick is complete. |
| `OnOrderValidation` | Filter\<AdapterOrderRequest\> | Filter an order request before any validation. |
| `OnOrderBeforeExecute` | Filter\<AdapterOrderRequest\> | Filter an order request just before execution. |
| `OnOrderAfterExecute` | Action\<(AdapterOrderRequest, AdapterOrderResponse)\> | Action after an order is executed or rejected. |
| `OnPositionOpened` | Action\<Position\> | Action when a new position is opened. |
| `OnPositionClosed` | Action\<Position\> | Action when a position is closed. |
| `OnPositionStopout` | Action\<Position\> | Action when a stop‑out occurs. |
| `OnEquityUpdated` | Action\<EquitySnapshot\> | Action when equity/drawdown is recalculated. |
| `OnCompleted` | Action | Fires when the backtest completes. |

#### Live Hooks (via `registry.Live`)

| Hook | Type | Description |
|------|------|-------------|
| `OnStart` | Action | Fires when a live session starts. |
| `OnTickReceived` | Filter\<Tick\> | Filter a tick as received from the adapter. |
| `OnTickProcessed` | Action\<Tick\> | Action after a tick is fully processed. |
| `OnOrderValidation` | Filter\<AdapterOrderRequest\> | Filter an order request before sending to exchange. |
| `OnOrderBeforeSend` | Filter\<AdapterOrderRequest\> | Filter an order immediately before sending. |
| `OnOrderExecuted` | Action\<ExecutionReport\> | Action when an execution report is received. |
| `OnOrderRejected` | Action\<(AdapterOrderRequest, string)\> | Action when an order is rejected. |
| `OnPositionOpened` | Action\<Position\> | Action when a new position is detected. |
| `OnPositionClosed` | Action\<Position\> | Action when a position is closed. |
| `OnPositionStopout` | Action\<Position\> | Action when a stop‑out occurs. |
| `OnSyncBefore` | Action | Action before periodic state sync. |
| `OnSyncAfter` | Action | Action after periodic state sync. |
| `OnReconnectAttempt` | Action\<int\> | Action on a reconnection attempt. |
| `OnReconnectSuccess` | Action\<int\> | Action on successful reconnection. |
| `OnStop` | Action | Fires when the live session stops. |
| `OnEquityChanged` | Action\<EquitySnapshot\> | Action when equity changes. |

#### Optimization Hooks (via `registry.Optimization`)

| Hook | Type | Description |
|------|------|-------------|
| `OnStart` | Action | Fires when optimization starts. |
| `OnGenerationStart` | Action\<int\> | Action at the start of each generation. |
| `OnChromosomeCreated` | Filter\<Chromosome\> | Filter a chromosome immediately after creation. |
| `OnChromosomeEvaluated` | Action\<(Chromosome, double)\> | Action after a chromosome's fitness is evaluated. |
| `OnSelectionApplied` | Action\<(Chromosome, Chromosome)\> | Action when parents are selected. |
| `OnCrossoverApplied` | Action\<Chromosome\> | Action when a child chromosome is produced. |
| `OnMutationApplied` | Action\<Chromosome\> | Action when a chromosome is mutated. |
| `OnGenerationCompleted` | Action\<(int, double, bool)\> | Action at the end of each generation. |
| `OnStagnationDetected` | Action\<int\> | Action when stagnation is detected. |
| `OnCompleted` | Action\<Chromosome\> | Fires when optimization completes. |
| `OnFitnessEvaluation` | Action\<IFitnessEvaluationContext\> | Hook for calculating fitness; plugins set `context.Fitness`. |

#### Report Hooks (via `registry.Report`)

| Hook | Type | Description |
|------|------|-------------|
| `OnBeforeGenerate` | Filter\<ReportRequest\> | Filter the report request before generation. |
| `OnAfterGenerate` | Action\<(byte[], string)\> | Action after a report is generated. |

### 6.7 Hook Contexts

Each hook callback receives an `IHookContext`, which provides:

| Member | Description |
|--------|-------------|
| `HookName` | The name of the hook being invoked. |
| `UtcNow` | UTC time at invocation (from the appropriate clock). |
| `CancellationToken` | Cancellation token for the operation. |

Specialized contexts provide additional data:

- `IBacktestContext` – `CurrentTick`, `TickIndex`, `TotalTicks`, `CurrentEquity`, `CurrentBalance`, `CurrentDrawdown`, `Broker`, `TickWindow`, `OpenPositions`
- `ILiveContext` – `CurrentTick`, `CurrentEquity`, `CurrentBalance`, `CurrentDrawdown`, `Broker`, `AdapterName`, `IsConnected`
- `IOptimizationContext` – `CurrentGeneration`, `TotalGenerations`, `PopulationSize`, `BestFitness`, `IsHyperMutation`
- `IReportContext` – `ReportFormat`

### 6.8 Example: Risk Management via Hooks

Instead of implementing a separate `IRiskManager`, register a filter on `backtest.order.validation`:

```csharp
public class DrawdownGuard : IHookManifest
{
    public void RegisterHooks(IHookRegistry registry)
    {
        registry.Backtest.OnOrderValidation.Register((order, ctx) =>
        {
            if (ctx is IBacktestContext bt && bt.CurrentDrawdown > 20.0)
                return FilterResult.Reject<AdapterOrderRequest>("Max drawdown exceeded");
            return FilterResult.Allow(order);
        }, priority: 10);
    }
}
```

### 6.9 Example: Custom Notifications via Hooks

The safe fire‑and‑forget pattern is demonstrated in §6.4. Here is a complete example:

```csharp
public class TelegramNotifier : IHookManifest
{
    public void RegisterHooks(IHookRegistry registry)
    {
        registry.Backtest.OnCompleted.Register(ctx =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await SendTelegramNotificationAsync("Backtest completed.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[TelegramNotifier] Failed: {ex.Message}");
                }
            });
        });
    }

    private async Task SendTelegramNotificationAsync(string message)
    {
        // Your HTTP call here...
        await Task.CompletedTask;
    }
}
```

### 6.10 Example: Custom Metrics via Hooks

```csharp
public class UlcerIndexMetric : IHookManifest
{
    private readonly List<double> _drawdowns = new();

    public void RegisterHooks(IHookRegistry registry)
    {
        registry.Backtest.OnEquityUpdated.Register((snapshot, ctx) =>
        {
            _drawdowns.Add(snapshot.Drawdown);
        }, priority: 100);

        registry.Backtest.OnCompleted.Register(ctx =>
        {
            double ulcer = Math.Sqrt(_drawdowns.Average(d => d * d));
            // The engine collects metrics from all registered hooks
        });
    }
}
```

### 6.11 Built‑in Audit Trail Logging

The engine automatically registers a built‑in **Audit Trail Logging** hook plugin (`Razor.Core.Engine.Hooks.AuditTrailLoggingHookPlugin`) on startup. It observes the live `OnOrderExecuted` and `OnOrderRejected` action hooks and writes an immutable, append‑only record of every broker order event to a SQLite database for regulatory compliance.

- **Storage:** a SQLite database whose path is configurable; it defaults to `./audit_trail.db` (relative to the process current working directory).
- **Immutability:** the `AuditTrail` table exposes only an `INSERT` path. Records can never be updated or deleted after they are written.
- **Records** contain: `TimestampUtc`, `TaskId`, `OrderId`, `Symbol`, `Side`, `Quantity`, `Price`, `Status` (`executed`/`rejected`), and `RejectionReason` (populated only for rejected orders).
- **Resilience:** failures while writing an audit record are caught and logged; they never throw into the live trading path.

This plugin requires no developer action. It is always active and survives extension reloads (the engine re‑registers it after clearing hook state). See `docs/Razor Future Features.md` → *Risk & Compliance → Audit Trail Logging* for the original roadmap entry.

---

## 7. Developing a Neural Network Model

Neural network models are slot capabilities. Implement `INeuralNetworkModel` (in `Razor.Core.Sdk.Slots.NeuralNetwork`) and place the DLL in the `NeuralNetworks/` directory. The engine activates the model when the active strategy declares `RequiresNeuralNetwork = true`.

### 7.1 The `INeuralNetworkModel` Interface

```csharp
public interface INeuralNetworkModel
{
    string ModelType { get; }
    int InputSize { get; }
    int OutputSize { get; }
    int ParameterCount { get; }
    double[] Predict(double[] inputs);
    void LoadParameters(double[] genes);
    double[] ExportParameters();
    void Reset();
    byte[] SerializeState();
    void DeserializeState(byte[] state);
}
```

### 7.2 Built‑In Feed‑Forward Network

Razor Kernel includes a built‑in `FeedForwardNetwork` implementation. If no custom model is provided and the strategy requires a neural network, the engine uses the built‑in feed‑forward network. The topology is determined by the strategy's gene schema.

### 7.3 Creating an ONNX Model

To use models trained in Python (PyTorch, TensorFlow), create an ONNX wrapper:

```csharp
public class OnnxModel : INeuralNetworkModel
{
    private InferenceSession _session;
    private double[] _parameters;

    public string ModelType => "ONNX";
    // ... implement all members using Microsoft.ML.OnnxRuntime
}
```

### 7.4 Creating an RL Model

For reinforcement learning, implement `INeuralNetworkModel` and use `Reset()` to start new episodes. The `Predict` method maps state observations to action Q‑values. The GA evolves the policy weights.

---

## 8. Best Practices

### 8.1 Performance

- `OnTick` is called millions of times in a backtest. **Avoid allocations** inside this method.
- Pre‑allocate arrays, use `Span<T>` if possible, and cache indicator references.
- Do not use `async`/`await` inside `OnTick` if your logic is CPU‑bound; keep it synchronous. (The broker's orders return immediately in simulation.)

### 8.2 Thread Safety for Adapters

- The engine may call your adapter methods from multiple threads simultaneously (e.g., a tick event and a periodic state sync). Use `SemaphoreSlim` or `lock` to protect shared state.
- The `OnExecutionUpdate` event handler can be invoked on any thread; ensure your event raising code is thread‑safe.

### 8.3 Determinism for Strategies and Hooks

- Do not use `System.Random` unless it's seeded deterministically and only for non‑trading purposes (e.g., logging).
- Use `CustomizedRandom` if you need a PRNG; seed it from the master seed passed through configuration or genes. **Use only non‑negative seeds** with the `int` constructor — negative seeds are rejected to prevent unpredictable sequences.
- Do not access `DateTime.UtcNow` or system clocks in trading logic.
- Hook callbacks execute deterministically by priority and name ordering. Do not rely on non‑deterministic behavior.

### 8.4 Avoiding Hard‑coded Market Logic

Your strategy should work across multiple asset classes if it does not depend on specific tick sizes or contract details. Let the broker handle those through `SymbolProperties`.

---

## 9. Deployment

### 9.1 Directory Placement

Place your compiled DLL in the appropriate directory:

| Extension Type | Directory |
|----------------|-----------|
| Adapter | `Adapters/` |
| Strategy | `Strategies/` |
| Indicator | `Indicators/` |
| Hook Plugin | `Plugins/` (or any directory—all are scanned) |
| NN Model | `NeuralNetworks/` |

### 9.2 Activation

1. The engine scans all directories on startup.
2. Discovered extensions are sent to Razor Cloud as a manifest.
3. The user selects active items from their Cloud profile.
4. The Cloud sends the active set to the engine.
5. Only active items are loaded and initialized.

### 9.3 Local Testing

During development, place your DLL in the appropriate directory of a locally running Razor Engine. The engine scans on startup, so restart after adding or updating DLLs. With a free development license, you can test adapters with a mock execution mode.

---

## 10. Versioning and Compatibility

### 10.1 Declaring Compatibility

Your extension assembly must include `[assembly: SdkVersion("1.0.0")]`. The engine's version manager checks this attribute.

- Extensions targeting an older major version may be loaded if backward‑compatible.
- Extensions targeting a newer major version are rejected unless an explicit compatibility mode is configured.

### 10.2 Breaking Changes

Razor follows SemVer. Minor releases add new hook points or interface members without breaking existing extensions. Major releases may break APIs, but older extensions can still run if they target an older SDK version (provided the engine supports that SDK major).

---

## 11. Platform‑Specific Notes

- **MetaTrader 5 Adapters:** MetaTrader 5 provides Windows‑only DLLs, so an MT5 adapter cannot run on Linux servers. If you develop an adapter for MT5, document this restriction clearly.
- **Other Brokers:** Most modern APIs (REST + WebSocket) are cross‑platform. Test your adapter on both Windows and Linux if you intend to support both.

---

## 12. Further Resources

- **Razor.Core.Sdk** NuGet package – contains XML documentation for every public member.
- **Razor Principles** – For a high‑level understanding of the engine's design rules.
- **Configuration Reference** – For all configuration objects and validation rules.
- **Installation & Deployment Guide** – For setting up the engine.