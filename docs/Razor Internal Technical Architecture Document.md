# Razor Internal Technical Architecture Document

**Version:** 1.0.0 LTS  
**Audience:** Razor core developers (Kernel, Engine, Cloud)  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

## 1. Introduction

This document describes the complete internal architecture of the Razor engine. It covers all closed‑source components (`Razor.Core.Kernel`, `Razor.Core.Engine`, and future `Razor.Cloud`) and explains how they interact with the public `Razor.Core.Sdk` and extensions.

It is the primary technical reference for:

- Modifying the core engine
- Building the Razor.Engine executable and Cloud backend
- Understanding data flows, threading, and determinism
- Integrating new subsystems

**It does not cover** the public SDK contracts—those are the domain of the Extension Developer Guide. Extension developers should never see this document.

Every design decision described here must comply with the **Razor Principles** (see `RazorPrinciples.md`). This document explains *how* those principles are implemented, not why they exist.

---

## 2. Solution Structure

### 2.1 Projects

The repository `Razor/` contains the following **source projects**:

| Project | Role | Visibility | Notes |
|---------|------|------------|-------|
| `Razor.Core.Sdk` | Public SDK contracts | Public (NuGet) | Hooks, slots, domain types, utilities. No runtime logic. |
| `Razor.Core.Shared` | Shared utilities (file I/O, memory mapping) | Private (closed‑source) | Memory‑mapped tick access, binary file mapping. |
| `Razor.Core.Kernel` | Core engine implementation | Private (closed‑source) | Backtesting, brokers, optimisation, telemetry, hook invoker. References `Razor.Core.Sdk` and `Razor.Core.Shared`. |
| `Razor.Core.Engine` | User‑facing executable | Private (closed‑source) | Bootstraps the engine, connects to Cloud, manages extension lifecycle. References all core projects. |
| `Razor.Cloud` | SaaS control plane | Private (closed‑source) | Web application for management, monitoring, reporting (future). |

### 2.2 Dependency Graph

```
Razor.Core.Sdk  (no dependencies beyond .NET 10 BCL)
       ↑
Razor.Core.Shared  (references Razor.Core.Sdk)
       ↑
Razor.Core.Kernel  (references Razor.Core.Sdk, Razor.Core.Shared)
       ↑
Razor.Core.Engine  (references all core projects)
```

Extensions (adapters, strategies, indicators, hook plugins, NN models) reference **only** `Razor.Core.Sdk`. The kernel never references extension assemblies directly; discovery is via reflection through isolated `AssemblyLoadContext`.

### 2.3 Repository Layout

```
Razor/
├── src/
│   ├── Razor.Core.Sdk/           ← Public contracts
│   │   ├── Hooks/                  ← Hook registration interfaces and context types
│   │   ├── Shared/                 ← Domain types, enums, exceptions, helpers
│   │   └── Slots/                  ← Capability interfaces (IAdapterCapability, IStrategyCapability, INeuralNetworkModel)
│   ├── Razor.Core.Shared/        ← Shared utilities
│   ├── Razor.Core.Kernel/        ← Core engine
│   └── Razor.Core.Engine/        ← Headless executable
├── tests/
│   ├── Razor.Core.Sdk.UnitTests/
│   ├── Razor.Core.Sdk.IntegrationTests/
│   ├── Razor.Core.Kernel.UnitTests/
│   ├── Razor.Core.Kernel.IntegrationTests/
│   └── Razor.Determinism.Tests/
├── docs/
├── Razor.Core.sln
├── Directory.Build.props
├── global.json
└── .editorconfig
```

---

## 3. Data Flow Architecture

Razor deals with ticks in two distinct modes, matching the principle that file‑based storage is only used when the volume demands it (backtesting/optimisation). The live path uses direct streaming with no file intermediary.

### 3.1 Historical Data Flow (Backtesting & Optimisation)

Used when the engine needs to replay large, static tick datasets.

```
Adapter.FetchHistoryToBinaryFileAsync()
        │
        ▼
  Binary file on disk  (magic/version header + raw Tick[])
        │
        ▼
  MemoryMappedTickList  (zero‑copy read‑only access)
        │
        ▼
  BorrowedTickData  (grouped streams + symbols)
        │
        ▼
  MergedTickTimeline  (single chronological stream)
        │
        ▼
  BacktestRunner / GA evaluator
        │
        ▼
  Disposal → NotifyFileSafeToDeleteAsync()
```

**Details:**

1. **Adapter fetches data:** The adapter implements `IAdapterCapability.FetchHistoryToBinaryFileAsync()`, downloading or converting historical data and writing it as a binary file using `BinaryDataMapper.WriteTicksToBinary()`. The file format is:
   - 8‑byte header: `uint32 magic = 0x53524843` ("CHRS"), `int32 version = 1`.
   - Followed by raw `Tick` structs (`[StructLayout(LayoutKind.Sequential, Pack=1)]`), each 33 bytes.
   - Ticks must be sorted by ascending `Time` (Principle 8). A debug assertion verifies this.

2. **Memory‑mapped access:** `MemoryMappedTickList` maps the file into virtual memory using `MemoryMappedFile`, bypassing the managed heap. It uses unsafe pointers for O(1) element access. The file is opened with `FileShare.Read` to allow concurrent reads. The finalizer releases only the raw pointer; managed handles (`MemoryMappedViewAccessor`, `MemoryMappedFile`) are finalized naturally by their own finalizers.

3. **Borrowed context:** `BorrowedTickData` holds an array of these mapped lists (one per symbol) and the corresponding file paths. It also holds a reference to the adapter via `IAdapterCapability`.

4. **Merge:** `MergedTickTimeline.EnumerateEvents()` uses a `PriorityQueue` to merge all streams into a single chronological sequence. Ties are broken by stream index for deterministic ordering. It enforces sorted input by throwing `InvalidOperationException` on out‑of‑order ticks.

5. **Execution:** The merged stream is consumed by `BacktestRunner` or the optimisation fitness evaluator.

6. **Cleanup:** After processing, `BorrowedTickData.DisposeAsync()` disposes the mapped lists (releasing the memory view) and calls `adapter.NotifyFileSafeToDeleteAsync()` for each file path. This enables adapter‑owned deletion (Principle 7).

### 3.2 Live Data Flow

Live ticks arrive asynchronously and are not stored in files.

```
Adapter (exchange WebSocket)
   │ OnTickReceived event (symbol, Tick)
   ▼
LiveBroker.OnTickReceived()
   │ (enqueues tick for processing)
   ▼
LiveBroker.ProcessTickAsync()
   │ (after feeding TickClock)
   ▼
TickClock.SetTickTime(tick.Time)
   │
   ├─→ Update broker state (prices, PnL, stop‑out, holding costs)
   ├─→ Periodic SyncStateAsync (balance/positions/orders)
   └─→ Strategy.OnTick(symbol, tick)
```

The adapter raises `IAdapterCapability.OnTickReceived`. The live broker subscribes, and each tick is enqueued to a dedicated processor thread. There is no file intermediary. The broker immediately updates its internal state and forwards the tick to the strategy, passing both the symbol and the tick data.

---

## 4. Clock System

Two implementations of `IClock` enforce the separation of market time and wall‑clock time (Principle 3).

### 4.1 TickClock

- **Location:** `Razor.Core.Kernel.Clock.TickClock`
- **Purpose:** All trading calculations (PnL, daily drawdown reset, holding cost timing, order execution timing in simulated broker).
- **Operation:** `SetTickTime(long timestamp)` is called at the very start of every tick processing. `GetTimestamp()` returns the last set value. `GetUtcNow()` converts it to a `DateTime`.
- **Used by:** `SimulatedBroker`, `LiveBroker` (for market operations), `BacktestRunner` (to advance the clock before each tick batch). Never by telemetry or scheduling.

### 4.2 SystemClock

- **Location:** `Razor.Core.Kernel.Clock.SystemClock`
- **Purpose:** Wall‑clock time for non‑trading concerns: in‑flight order guards, telemetry timestamps, health checks.
- **Operation:** `GetTimestamp()` returns `Environment.TickCount64` (monotonic, unaffected by system time adjustments). `GetUtcNow()` returns `DateTime.UtcNow` (only used for logging/events).
- **Used by:** `LiveBroker` (order guard timeouts, telemetry), `CoreMetrics` (recording latencies), and scheduling logic.

**Enforcement:** Any call to `DateTime.UtcNow` inside `Razor.Kernel` trading paths is a build‑breaking violation per static analysis CI.

---

## 5. Broker Architecture

Both brokers implement `IBroker` and use the same `IMarketCalculator` for financial math, ensuring live‑backtest parity (Principle 5).

### 5.1 SimulatedBroker

Used exclusively for backtesting and optimisation. Entirely deterministic, single‑threaded per backtest run.

**Key characteristics:**

- **State:** Protected by a `Lock` object; all public methods and `OnTickAsync` lock.
- **Mutable internal positions:** Uses a private `MutablePosition` class to avoid record copying overhead during updates. Immutable `Position` snapshots are returned to callers.
- **Order lifecycle:**
  - Market orders: If `latencyTicks > 0`, enqueued in `_executionQueue` and executed when `TickClock` reaches the scheduled time. Otherwise executed instantly.
  - Pending orders: Stored in `List<Order>`; checked on each tick via `IMarketCalculator.IsPendingOrderTriggered()`. When triggered, converted to a position using the trigger price adjusted by slippage.
- **SL/TP:** Evaluated for the symbol of the just‑arrived tick, using the appropriate bid/ask.
- **Stop‑out:** If `Equity / MarginUsed ≤ _stopOutLevel`, the position with the worst floating PnL is force‑closed.
- **Holding costs:** `ProcessHoldingCosts()` charges daily swap/funding based on `TickClock` time. Resets daily peak equity at UTC day boundaries.
- **Friction:** Slippage and commission are derived from the adapter's `IMarketCalculator` and `SymbolProperties`. There is no separate `ISimulationFriction`; the adapter owns all friction logic.
- **Partial closes:** Supported; adjusts volume and apportions commission/swap.
- **Warm‑up:** `IsWarmup` property is set by the backtest runner. While true, all order methods return a rejection response.

**Determinism:** No `DateTime.UtcNow`, no system clock. All randomisation is external (strategy can be seeded). The execution queue time is purely tick‑driven.

### 5.2 LiveBroker

Wraps an `IAdapterCapability` for real exchange trading. Adds reconciliation, connection handling, and telemetry.

**Key characteristics:**

- **State lock:** `SemaphoreSlim(1,1)` ensures thread‑safe access (ticks, order responses, periodic sync).
- **Tick handling:** `ProcessTickAsync()` updates `_lastPrices`, processes holding costs, recalculates floating PnL for all positions, checks SL/TP, and triggers stop‑out. It also periodically calls `SyncStateAsync()`.
- **State reconciliation:** `ReconcileAsync()` fetches the full account state from the adapter and corrects local positions/orders. Called at startup and after reconnection.
- **In‑flight order guard:** Uses `SystemClock.GetTimestamp()` (monotonic) + configurable timeout to prevent duplicate order submissions.
- **Order execution:** Delegated to adapter methods. Responses are returned immediately; execution reports are handled asynchronously.
- **Execution reports:** The adapter's `OnExecutionUpdate` event is handled in a fire‑and‑forget task with full exception logging to prevent process crashes (Principle 13).
- **Connection management:** `ConnectAndNotifyAsync` and `DisconnectAndNotifyAsync` publish `ConnectionStateEvent` and update telemetry.
- **Telemetry:** Records order latency, rejection count, and tick arrival latency via `CoreMetrics`.
- **Logger:** Uses `ILogger<LiveBroker>` for operational visibility.

**Parity with SimulatedBroker:** Both use the same `IMarketCalculator`, the same stop‑out logic, the same SL/TP evaluation order, and the same daily holding cost calculation. Unit tests verify that identical tick sequences produce identical trade histories.

---

## 6. Backtesting Engine

### 6.1 Components

- **`BacktestInput`** – Immutable record containing all necessary data: tick streams, symbols, strategy, specs, calculator, optional genes and neural network, progress reporter, message bus.
- **`BacktestRunner`** – The orchestrator implementing `IBacktestRunner`.
- **`BacktestProgress` / `BacktestResult`** – Data transfer records.
- **`MergedTickTimeline`** – Merges multiple tick streams into one chronological enumerator.

### 6.2 Execution Flow

1. **Setup:**
   - Creates a `TickClock`.
   - Instantiates `SimulatedBroker` with `IMarketCalculator`, symbol properties, etc.
   - Creates `TickWindow` for the requested timeframes (excluding `Tick`).
   - Wires broker and tick window to the strategy via `StrategyBase.WireUp()`.

2. **Gene injection:**
   - If `input.Genes` is provided, uses them directly.
   - Otherwise calls `GeneInjector.ExtractAndInitializeGenes()` to produce a deterministic gene set using the `GeneInitializationSeed` from `ExecutionSpecification`. The seed is nullable; if `null`, no explicit seed was provided and genes are taken from the strategy's defaults.

3. **Strategy lifecycle:**
   - Calls `OnConfigureAsync()` then `OnStartAsync()`. Both are sync‑over‑async because the loop must remain synchronous for determinism (no real I/O inside strategy for backtest).

4. **Main loop:**
   - Iterates over the merged enumerator.
   - For each event:
     - Sets `TickClock` to the tick's time.
     - Invokes the `backtest.tick.received` filter hook chain.
     - Calls `broker.OnTickAsync(symbol, tick)` (synchronous, lock‑protected).
     - Invokes the `backtest.tick.strategy_before` filter hook chain.
     - Pushes tick into `TickWindow`.
     - Calls `strategy.OnTick(symbol, tick)`.
     - Invokes the `backtest.tick.strategy_after` action hook.
     - Invokes the `backtest.tick.completed` action hook.

5. **Teardown:**
   - Closes all open positions per symbol.
   - Disposes indicators.
   - Calls `OnStopAsync()`.

6. **Result assembly:**
   - Collects trade history from broker.
   - Calculates metrics via `MetricsCalculator`.
   - Publishes `BacktestCompletedEvent` with full statistics.
   - Invokes the `backtest.completed` action hook.
   - Records throughput telemetry.

**Determinism:** The entire loop uses no wall‑clock time, no `Random`, and no mutable external state. All gene seeds are derived from the master seed. Given identical tick streams and seeds, the output is bit‑identical.

---

## 7. Hook System Architecture

### 7.1 Overview

The hook system is the primary extensibility mechanism. Instead of many typed plugin interfaces (`IRiskManager`, `IFitnessModel`, `INotificationChannel`, etc.), the engine exposes named hook points. Extensions implement `IHookManifest` and register strongly‑typed callbacks on these points.

This replaces the earlier, more rigid plugin interfaces:
- **Risk management** – previously `IRiskManager`, now achieved via filter hooks on order validation (`backtest.order.validation`, `live.order.validation`).
- **Fitness evaluation** – previously `IFitnessModel`, now achieved via the `optimization.fitness.evaluate` action hook.
- **Execution algorithms** – previously `IExecutionAlgorithm`, now achieved via filter hooks on order before execution (`backtest.order.before_execute`, `live.order.before_send`).
- **Simulation friction** – previously `ISimulationFriction`, now handled internally by the adapter's `IMarketCalculator` and order execution logic; slippage and commission are adapter‑owned.

### 7.2 Hook Types

- **Filter hooks** (`IFilterRegistration<T>`): Transform or reject data flowing through the pipeline. Each callback receives the current value and context, returning a `FilterResult<T>` indicating whether to allow (possibly modified) or reject.
- **Action hooks** (`IActionRegistration<T>` or `IActionRegistration`): Observe events without modifying data. Typed variants receive event data; parameterless variants receive only the context. All action callbacks are invoked synchronously; `async void` is forbidden as exceptions would crash the process.

### 7.3 Hook Registry

The engine creates an implementation of `IHookRegistry` at startup. Extension assemblies implementing `IHookManifest` receive this registry and register their callbacks. The registry is organized into four sub‑registries:

| Sub‑Registry | Pipeline |
|--------------|----------|
| `IBacktestHooks` | Backtesting |
| `ILiveHooks` | Live trading |
| `IOptimizationHooks` | Genetic optimisation |
| `IReportHooks` | Report generation |

Each sub‑registry exposes typed registration properties for each hook point (e.g., `IBacktestHooks.OnOrderValidation`, `ILiveHooks.OnTickReceived`).

### 7.4 Hook Invocation

Hook invocations are synchronous and deterministic. For each hook point, the engine:

1. Retrieves the ordered list of registered callbacks.
2. For filter hooks: applies each callback in order, passing the result of the previous callback as input to the next. If any callback returns `IsAllowed = false`, the chain stops and the rejection is returned.
3. For action hooks: invokes each callback in order. Exceptions in action callbacks are caught and logged; they never stop the chain.

### 7.5 Priority Ordering

Callbacks are ordered deterministically:
1. Priority (lower = earlier execution)
2. Plugin name (alphabetical, case‑insensitive ordinal)
3. Registration order within the plugin

This guarantees bit‑identical hook execution order across runs.

---

## 8. Genetic Optimisation Engine

### 8.1 Components

- **`GeneticOptimizer`** – Steppable GA implementing `IGeneticOptimizer`. Host controls generation flow.
- **`Chromosome`** – A single candidate solution. Contains a double[] gene array, fitness, generation index, seed. The sentinel `Chromosome.NotEvaluated` (`double.NegativeInfinity`) marks unevaluated chromosomes.
- **`GeneInjector`** – Static class in `Razor.Core.Sdk.Shared` for extracting schemas, injecting genes into strategy properties and neural network models, and building complete chromosome arrays.
- **`OptimizationSpecification`** – Immutable configuration (population size, mutation rate, etc.) in `Razor.Core.Kernel.Configuration`.
- **`GeneticOptimizerState`** – Serializable state for pause/resume.

### 8.2 Chromosome Schema

A chromosome is a flat `double[]` built from:

1. **Strategy properties** decorated with `[Gene]` attributes (min, max, step, type). **Note:** The `Step` parameter is only valid for `Discrete` and `Categorical` gene types; for `Continuous`, `Structural`, and `Parametric` it must be `0`, enforced by the `GeneAttribute` constructor.
2. **Neural network parameters** (if an `INeuralNetworkModel` is active), appended after the property genes. The parameter count is obtained from `INeuralNetworkModel.ParameterCount`.

The schema is extracted via `GeneInjector.BuildCompleteSchema()`. The total gene count is deterministic.

### 8.3 Initialization

- **Master seed** from `OptimizationSpecification.MasterSeed` (must be `≥ 0`).
- **Per‑individual seed** generated with `(masterSeed * 397) ^ index`, avoiding platform‑dependent `HashCode`.
- **RNG** is `CustomizedRandom` (portable xorshift128+). The `int` constructor rejects negative seeds to guarantee predictable sequences.
- Each gene is randomly chosen within its constraints using `GeneInjector.GenerateRandomGene()`.

### 8.4 Evaluation

- Host calls `EvaluateAsync()` with a fitness function delegate.
- Evaluation is parallelised using `Parallel.ForEachAsync` with configurable `MaxDegreeOfParallelism`.
- A typical evaluator runs a full `BacktestRunner` with the chromosome's genes injected into the strategy, then invokes the `optimization.chromosome.evaluated` action hook for fitness computation.
- Fitness values are assigned directly to each chromosome.

### 8.5 Evolution

- **Selection:** Tournament selection (size `TournamentSize`) using the main RNG.
- **Crossover:** Uniform crossover with probability `CrossoverRate`. Genes come from parent1 or parent2.
- **Mutation:** Each gene has a `mutationRate` chance of being randomised. If hyper‑mutation is active, the rate is tripled (capped at 1.0).
- **Elitism:** The best `ElitismPct * PopulationSize` individuals are copied unchanged to the next generation.
- **Stagnation detection:** If the best fitness does not improve for `StagnationGenerationsBeforeHyper` consecutive generations, hyper‑mutation is activated to escape local optima.

### 8.6 State Serialization

`SaveState()` produces a `GeneticOptimizerState` record containing a deep clone of the entire population. `LoadState()` restores it. After loading, call `InvalidateFitness()` if the evaluation data has changed.

---

## 9. Configuration & Specification System

All configuration is represented by immutable `record` types that implement a `Validate()` method. A `ConfigurationException` is thrown for invalid input. There are no silent defaults for critical parameters (Principle 9).

**Specifications in `Razor.Core.Kernel.Configuration`:**

- `ExecutionSpecification` – date range, warmup, latency, max positions, stop‑out, parallelism, gene seed (nullable `int?`).
- `OptimizationSpecification` – master seed (`≥ 0`), population/generations, mutation/crossover rates, elitism, tournament size. No walk‑forward fields (orchestration is Cloud‑managed). No `FitnessModel` field (fitness is computed via hooks).
- `LiveSpecification` – magic number, order guard timeout. Continuous optimisation fields removed (Cloud‑orchestrated).

**Specifications in `Razor.Core.Sdk.Shared`:**

- `StrategySpecification` – initial balance, leverage, symbols/timeframes. No `FrictionModel` (adapter‑internal) or `FitnessModel` (hook‑based). Duplicate symbols are rejected.

Each has a static `CreateValidated(...)` factory that performs validation immediately.

**Parsing:** The engine never parses user input (timeframes, etc.) from strings—this is the responsibility of the Cloud. The kernel receives already‑parsed types.

---

## 10. Extension Loading & Versioning

### 10.1 Directory Structure

Extensions are placed in subdirectories alongside the engine:

| Directory | Purpose | Scanned For |
|-----------|---------|-------------|
| `Adapters/` | Broker connectivity | `IAdapterCapability` |
| `Strategies/` | Trading logic | `IStrategyCapability` |
| `Indicators/` | Technical analysis | `Indicator` subclasses |
| `Plugins/` | Hook‑based extensions | `IHookManifest` |
| `NeuralNetworks/` | Neural network models | `INeuralNetworkModel` |

A single DLL can implement any combination. The engine scans all directories.

### 10.2 Version Attributes

- `[assembly: SdkVersion("1.0.0")]` – declares the targeted SDK version. The engine checks this before loading any assembly.

### 10.3 Loading Process

1. Scan all extension directories for `.dll` files.
2. For each assembly, load in a new `PluginLoadContext` (unloadable later).
3. The `PluginLoadContext` ensures `Razor.Core.Sdk` is loaded from the default context (type sharing), while all other dependencies are resolved from the extension's directory.
4. Call `PluginValidator.ValidateAssembly()` to check the SDK version. Reject if the major version differs.
5. Call `PluginSafetyValidator.Validate()` to check strong‑naming in production.
6. Discover types implementing `IHookManifest`, `IAdapterCapability`, `IStrategyCapability`, `INeuralNetworkModel`, or `Indicator`.
7. Register discovered items in the engine's manifest.
8. Send the manifest to Razor Cloud.
9. Cloud responds with the active set (selected by the user from their profile).
10. Activate the selected adapter, strategy, indicators, NN model, and hook plugins.

**Isolation:** Each extension context resolves dependencies independently, avoiding version conflicts. Assemblies must be strongly signed in production; unsigned plugins are rejected.

---

## 11. Messaging & Events

### 11.1 In‑Process Message Bus

`Razor.Kernel.Messaging.MessageBus` implements `IMessageBus`:

- **Typed subscriptions:** Handlers are stored per message type.
- **Deduplication:** If `message.EventId` is non‑null and has been published within the last 60 seconds, the message is suppressed.
- **Thread safety:** Subscriptions are locked; publishing iterates a snapshot of handlers.

### 11.2 Event Catalog

All event records reside in `Razor.Core.Kernel.Events`. They are internal infrastructure and not part of the public SDK.

| Event | Publisher | Payload |
|-------|-----------|---------|
| `BacktestStartedEvent` | BacktestRunner | (timestamp only) |
| `BacktestCompletedEvent` | BacktestRunner | NetProfit, ReturnPct, MaxDrawdown, Sharpe, Sortino, etc. |
| `OrderExecutedEvent` | SimulatedBroker, LiveBroker | Symbol, OrderType, Volume, Price, IsOpen |
| `ConnectionStateEvent` | LiveBroker | IsConnected, AdapterName |
| `LiveReconnectEvent` | LiveBroker | Success, AttemptCount, AdapterName |
| `LiveSessionEndedEvent` | LiveBroker | FinalBalance, FinalEquity, MaxDrawdown, TotalTrades |
| `OptimizationGenerationEvent` | OptimizationRunner | Generation, BestFitness, IsHyperMutation |
| `OptimizationCycleCompletedEvent` | OptimizationRunner | CycleIndex, BestFitness, GenerationCount |

---

## 12. Threading & Concurrency

| Component | Threading Model | Notes |
|-----------|-----------------|-------|
| `BacktestRunner` | Single‑threaded | Sync‑over‑async; no concurrency. |
| `SimulatedBroker` | Lock‑protected | All public methods and `OnTickAsync` use `_stateLock`. |
| `LiveBroker` | `SemaphoreSlim(1,1)` | Protects all state. Dedicated tick processor thread. |
| `GeneticOptimizer.EvaluateAsync` | Parallel | Uses `Parallel.ForEachAsync` with configurable max DOP. |
| `MessageBus` | Lock‑free for publish | Uses snapshot of handlers; subscriptions locked briefly. |
| `HookInvoker` | Single‑threaded per pipeline | Filters and actions execute synchronously within the pipeline's thread. |
| Adapter implementations | Must be thread‑safe (Principle 13) | The engine may call adapter methods from multiple threads. |
| `TickWindow` | Not thread‑safe | Designed for single‑threaded tick processing only. |

---

## 13. Telemetry & Observability

`CoreMetrics` (instance‑based) provides OpenTelemetry metrics via `System.Diagnostics.Metrics`.

| Metric | Instrument | Description |
|--------|------------|-------------|
| `core.ga.fitness_improvement` | Histogram | Improvement in best fitness per generation |
| `core.backtest.ticks_per_second` | Histogram | Tick processing rate |
| `core.live.order_latency_ms` | Histogram | Order placement latency in milliseconds |
| `core.live.order_rejections_total` | Counter | Total order rejections |
| `core.optimization.duration_seconds` | Histogram | Total optimisation run duration |
| `core.live.tick_latency_ticks` | Histogram | Live tick arrival latency |
| `core.live.connection_state` | Gauge | 1 if connected, 0 if disconnected |

All metrics are registered in a `Meter` named `"Core.Metrics"`. Engine‑specific metrics are in `EngineTelemetry` under `"Razor.Core.Engine"`.

---

## 14. Report Generation (Engine Role)

The engine does **not** generate formatted reports (PDF, HTML, Excel, etc.). Report rendering is the responsibility of Razor Cloud. The engine's role is limited to:

1. Streaming raw `BacktestResult` and `Chromosome` data to the Cloud via events (`BacktestCompletedEvent`, `OptimizationGenerationEvent`, etc.).
2. Providing the `ReportGenerator` class, which exists solely to invoke the `report.before_generate` and `report.after_generate` hooks. These hooks allow plugins to capture or modify the raw data before it is sent to Cloud, or to perform custom logging.

**Summary:** The engine streams raw data; the Cloud renders reports.

---

## 15. Determinism Infrastructure

### 15.1 Random Number Generation

`CustomizedRandom` is a custom xorshift128+ implementation that guarantees identical sequences across .NET versions and platforms. It is used for:

- GA population initialisation
- GA selection, crossover, and mutation
- Deterministic gene initialisation for backtests
- Synthetic tick generation (optional)

`System.Random` is never used in any path that affects backtest output or optimisation results. Negative seeds are rejected by `CustomizedRandom`'s `int` constructor to avoid confusion.

### 15.2 Seeding Strategy

- A **master seed** is provided by the user (through configuration). All randomness derives from this seed. It must be non‑negative.
- Per‑individual GA seeds are generated with `(masterSeed * 397) ^ index` — stable across .NET versions.
- Backtest gene seeds are generated from the nullable `GeneInitializationSeed` in `ExecutionSpecification`.

### 15.3 System Clock Prohibition

No trading logic accesses `DateTime.UtcNow` or `Environment.TickCount64`. The only exceptions are the `SystemClock` used for order guards, telemetry, and logging, all of which are non‑trading concerns.

### 15.4 Golden Tests

A separate test suite (CI gate) executes a full backtest twice with identical inputs and compares the hash of the serialised trade history. Any difference fails the build. This validates determinism across code changes and .NET updates.

---

## 16. Project: Razor.Core.Engine

### 16.1 Architecture Overview

The `Razor.Core.Engine` project is the headless executable that hosts the kernel and communicates with Razor Cloud. Its key components are:

- **Program.cs** – Entry point, CLI parsing, service registration, shutdown handling.
- **CloudConnector** – WebSocket connection, authentication, heartbeat, command dispatch, binary transfers.
- **SecurityManager** – ECDH key exchange, AES‑256‑GCM encryption, integrity checks.
- **StateManager** – SQLite persistence for engine ID, live state, optimisation state, cron jobs, schedules, queued messages.
- **ExtensionManager** – Discovery, activation, isolation, hot‑reload.
- **TaskManager** – Task scheduling with live‑first priority.
- **CronJobManager** – Cron and one‑off schedule execution.
- **KernelService** – Facade bridging engine commands to kernel operations.
- **BehaviorRecorder** – Sparse behavior logging with MessagePack + GZip.
- **SelfUpdateManager** – Download, checksum, staging, and rollback.

### 16.2 Dependency Injection

All services are registered in `BuildServiceProvider()` using `Microsoft.Extensions.DependencyInjection`. Key registrations include:

- `Singleton` for stateless services (SecurityManager, StateManager, CloudConnector, etc.)
- `Scoped` for task‑specific services
- `Lazy<T>` for dependencies that must be resolved after construction (e.g., ICloudConnector in BehaviorRecorder)

### 16.3 Command Flow

1. Cloud sends a `Command` message.
2. `CloudConnector.ReceiveLoopAsync()` deserializes and dispatches to `ProcessReceivedMessageAsync()`.
3. `CommandReceived` event fires.
4. `CommandDispatcher.OnCommandReceivedAsync()` calls `DispatchAsync()`.
5. `DispatchAsync()` looks up the handler by `CommandId` and invokes `HandleAsync()`.
6. Handler executes the operation and sends a `CommandResponse` via `SendResponseAsync()`.

---

## 17. Future Projects (Out of Scope for v1.0.0 LTS)

### 17.1 Razor.Cloud

- SaaS web application.
- Sends commands, receives progress/results, stores all data.
- Provides dashboards for monitoring, reporting, and configuration.
- Manages user accounts, licenses, extension deployment.
- Stores user profiles with active adapter, strategy, indicators, and hook plugin selections per engine instance.

Both will be private repositories, developed after the stable v1.0.0 kernel release.

---

*This document is the authoritative internal reference. Any architecture deviation must be approved by the Razor architecture board.*