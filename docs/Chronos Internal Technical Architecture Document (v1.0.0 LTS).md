# Chronos Internal Technical Architecture Document

**Version:** 1.0.0 LTS
**Audience:** Chronos core developers (Kernel, Engine, Cloud)
**Status:** Authoritative
**Last Updated:** 2026-05-19

---

## 1. Introduction

This document describes the complete internal architecture of the Chronos engine. It covers all closed‑source components (`Chronos.Kernel`, future `Chronos.Engine`, `Chronos.Cloud`) and explains how they interact with the public `Chronos.Abstractions` SDK and adapters.

It is the primary technical reference for:

- Modifying the core engine
- Building the Chronos.Engine executable and Cloud backend
- Understanding data flows, threading, and determinism
- Integrating new subsystems

**It does not cover** the public SDK contracts—those are the domain of the Plugin Developer Guide. Plugin developers should never see this document.

Every design decision described here must comply with the **Chronos Principles** (see `ChronosPrinciples.md`). This document explains *how* those principles are implemented, not why they exist.

---

## 2. Solution Structure

### 2.1 Projects

The repository `Chronos/` contains the following **source projects**:

| Project | Role | Visibility | Notes |
|---------|------|------------|-------|
| `Chronos.Abstractions` | Public SDK contracts | Public (NuGet) | Interfaces, records, enums, utilities. No runtime logic. |
| `Chronos.Kernel` | Core engine implementation | Private (closed‑source) | Backtesting, brokers, optimisation, telemetry. References `Chronos.Abstractions`. |
| *(future)* `Chronos.Engine` | User‑facing executable | Private (closed‑source) | Bootstraps the engine, connects to Cloud, manages plugins and lifecycle. Will replace `Hosts.Console`. |
| *(future)* `Chronos.Cloud` | SaaS control plane | Private (closed‑source) | Web application for management, monitoring, reporting. |
| `Chronos.Samples` | Example plugins | External repo | Demonstrates adapter and strategy implementation. Not part of the engine build. |

**Discontinued projects** (already removed or pending removal):

- `Chronos.Orchestration` – temporary test project; to be deleted.
- `Chronos.Messaging` – merged into Kernel.
- `Chronos.Sdk` / `Chronos.Core` – old naming, replaced by `Chronos.Abstractions` and `Chronos.Kernel`.

The solution currently includes `Hosts.Console` for development convenience; it will be replaced by `Chronos.Engine`.

### 2.2 Dependency Graph

```
Chronos.Abstractions  (no dependencies beyond .NET 10 BCL)
       ↑
Chronos.Kernel        (references Chronos.Abstractions)
       ↑
Chronos.Engine        (references Chronos.Kernel, Chronos.Abstractions)
```

Plugins (adapters, strategies) reference **only** `Chronos.Abstractions`. The kernel never references plugin assemblies directly; discovery is via reflection through isolated `AssemblyLoadContext`.

### 2.3 Repository Layout

```
Chronos/
├── src/
│   ├── Chronos.Abstractions/
│   ├── Chronos.Kernel/
│   ├── Chronos.Samples/           # Separate repo in production, here for development
│   └── Hosts.Console/             # Temporary, will be removed
├── tests/
│   ├── Chronos.Kernel.Tests/
│   └── Chronos.Determinism.Tests/
├── docs/
├── Chronos.sln
├── Directory.Build.props
├── global.json
└── .editorconfig
```

---

## 3. Data Flow Architecture

Chronos deals with ticks in two distinct modes, matching the principle that file‑based storage is only used when the volume demands it (backtesting/optimisation). The live path uses direct streaming with no file intermediary.

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

1. **Adapter fetches data:** The adapter’s `IHistoricalDataProvider.FetchHistoryToBinaryFileAsync()` downloads or converts historical data and writes it as a binary file using `BinaryDataMapper.WriteTicksToBinary()`. The file format is:
   - 8‑byte header: `uint32 magic = 0x53524843` ("CHRS"), `int32 version = 1`.
   - Followed by raw `Tick` structs (`[StructLayout(LayoutKind.Sequential, Pack=1)]`), each 33 bytes.
   - Ticks must be sorted by ascending `Time` (Principle 8). A debug assertion verifies this.

2. **Memory‑mapped access:** `MemoryMappedTickList` maps the file into virtual memory using `MemoryMappedFile`, bypassing the managed heap. It uses unsafe pointers for O(1) element access. The file is opened with `FileShare.Read` to allow concurrent reads.

3. **Borrowed context:** `BorrowedTickData` holds an array of these mapped lists (one per symbol) and the corresponding file paths. It also holds a reference to the adapter.

4. **Merge:** `MergedTickTimeline.EnumerateEvents()` uses a `PriorityQueue` to merge all streams into a single chronological sequence. Ties are broken by stream index for deterministic ordering.

5. **Execution:** The merged stream is consumed by `BacktestRunner` or the optimisation fitness evaluator.

6. **Cleanup:** After processing, `BorrowedTickData.DisposeAsync()` disposes the mapped lists (releasing the memory view) and calls `adapter.NotifyFileSafeToDeleteAsync()` for each file path. This enables adapter‑owned deletion (Principle 7).

### 3.2 Live Data Flow

Live ticks arrive asynchronously and are not stored in files.

```
Adapter (exchange WebSocket)
   │ OnTickReceived event (symbol, Tick)
   ▼
LiveBroker.OnTickAsync()
   │ (after feeding TickClock)
   ▼
TickClock.SetTickTime(tick.Time)
   │
   ├─→ Update broker state (prices, PnL, stop‑out, holding costs)
   ├─→ Periodic SyncStateAsync (balance/positions/orders)
   └─→ Strategy.OnTickAsync(tick)
```

The adapter raises `ILiveDataProvider.OnTickReceived`. The live broker subscribes, and each tick is pushed directly into memory. There is no file intermediary. The broker immediately updates its internal state and forwards the tick to the strategy.

---

## 4. Clock System

Two implementations of `IClock` enforce the separation of market time and wall‑clock time (Principle 3).

### 4.1 TickClock

- **Location:** `Chronos.Kernel.Clock.TickClock`
- **Purpose:** All trading calculations (PnL, daily drawdown reset, holding cost timing, order execution timing in simulated broker).
- **Operation:** `SetTickTime(long timestamp)` is called at the very start of every tick processing. `GetTimestamp()` returns the last set value. `GetUtcNow()` converts it to a `DateTime`.
- **Used by:** `SimulatedBroker`, `LiveBroker` (for market operations), `BacktestRunner` (to advance the clock before each tick batch). Never by telemetry or scheduling.

### 4.2 SystemClock

- **Location:** `Chronos.Kernel.Clock.SystemClock`
- **Purpose:** Wall‑clock time for non‑trading concerns: in‑flight order guards, telemetry timestamps, health checks.
- **Operation:** `GetTimestamp()` returns `Environment.TickCount64` (monotonic, unaffected by system time adjustments). `GetUtcNow()` returns `DateTime.UtcNow` (only used for logging/events).
- **Used by:** `LiveBroker` (order guard timeouts, telemetry), `ChronosMetrics` (recording latencies), and future scheduling logic.

**Enforcement:** Any call to `DateTime.UtcNow` inside `Chronos.Kernel` trading paths is a build‑breaking violation per static analysis CI.

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
- **Friction:** `ISimulationFriction` applies slippage (against the trader) and commission on every fill.
- **Partial closes:** Supported; adjusts volume and apportions commission/swap.

**Determinism:** No `DateTime.UtcNow`, no system clock. All randomisation is external (strategy can be seeded). The execution queue time is purely tick‑driven.

### 5.2 LiveBroker

Wraps an `IAdapter` for real exchange trading. Adds reconciliation, connection handling, and telemetry.

**Key characteristics:**

- **State lock:** `SemaphoreSlim(1,1)` ensures thread‑safe access (ticks, order responses, periodic sync).
- **Tick handling:** `OnTickAsync` updates `_lastPrices`, processes holding costs, recalculates floating PnL for all positions, checks SL/TP, and triggers stop‑out. It also periodically calls `SyncStateAsync()`.
- **State reconciliation:** `ReconcileAsync()` fetches the full account state from the adapter and corrects local positions/orders. Called at startup and after reconnection.
- **In‑flight order guard:** Uses `SystemClock.GetTimestamp()` (monotonic) + configurable timeout to prevent duplicate order submissions. Protects against rapid double‑clicks or network retries.
- **Order execution:** Delegated to adapter methods. Responses are returned immediately; execution reports are handled asynchronously.
- **Execution reports:** The adapter’s `OnExecutionUpdate` event is handled in an `async void` wrapper with full exception logging to prevent process crashes (Principle 13).
- **Connection management:** `ConnectAndNotifyAsync` and `DisconnectAndNotifyAsync` publish `ConnectionStateEvent` and update telemetry.
- **Telemetry:** Records order latency, rejection count, and tick arrival latency via `ChronosMetrics`.

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
   - Instantiates `SimulatedBroker` with `IMarketCalculator`, friction model, symbol properties, etc.
   - Creates `TickWindow` for the requested timeframes (excluding `Tick`).
   - Wires broker and tick window to the strategy via `StrategyBase.WireUp()`.

2. **Gene injection:**
   - If `input.Genes` is provided, uses them directly.
   - Otherwise calls `GeneInjector.ExtractAndInitializeGenes()` to produce a deterministic gene set using the `GeneInitializationSeed`.

3. **Strategy lifecycle:**
   - Calls `OnConfigureAsync()` then `OnStartAsync()`. Both are sync‑over‑async because the loop must remain synchronous for determinism (no real I/O inside strategy for backtest).

4. **Main loop:**
   - Iterates over the merged enumerator.
   - For each event:
     - Sets `TickClock` to the tick’s time.
     - Calls `broker.OnTickAsync(symbol, tick)` (synchronous, lock‑protected).
     - Pushes tick into `TickWindow`.
     - Calls `strategy.OnTickAsync(tick)`.

5. **Teardown:**
   - Closes all open positions per symbol.
   - Disposes indicators.
   - Calls `OnStopAsync()`.

6. **Result assembly:**
   - Collects trade history from broker.
   - Calculates metrics via `MetricsCalculator`.
   - Publishes `BacktestCompletedEvent` with full statistics.
   - Records throughput telemetry.

**Determinism:** The entire loop uses no wall‑clock time, no `Random`, and no mutable external state. All gene seeds are derived from the master seed. Given identical tick streams and seeds, the output is bit‑identical.

---

## 7. Genetic Optimisation Engine

### 7.1 Components

- **`GeneticOptimizer`** – Steppable GA implementing `IGeneticOptimizer`. Host controls generation flow.
- **`Chromosome`** – A single candidate solution. Contains a double[] gene array, fitness, generation index, seed.
- **`GeneInjector`** – Static class for extracting schemas, injecting genes into strategy properties and neural networks, building complete chromosome arrays.
- **`OptimizationSpecification`** – Immutable configuration (population size, mutation rate, etc.).
- **`GeneticOptimizerState`** – Serializable state for pause/resume.

### 7.2 Chromosome Schema

A chromosome is a flat `double[]` built from:

1. **Strategy properties** decorated with `[Gene]` attributes (min, max, step, type).
2. **Neural network weights and biases** (if a `FeedForwardNetwork` is specified), appended after the property genes.

The schema is extracted via `GeneInjector.BuildCompleteSchema()`. The total gene count is deterministic.

### 7.3 Initialization

- **Master seed** from `OptimizationSpecification.MasterSeed`.
- **Per‑individual seed** generated with `(masterSeed * 397) ^ index`, avoiding platform‑dependent `HashCode`.
- **RNG** is `ChronosRandom` (portable xorshift128+).
- Each gene is randomly chosen within its constraints using `GeneInjector.GenerateRandomGene()`.

### 7.4 Evaluation

- Host calls `EvaluateAsync()` with a fitness function delegate.
- Evaluation is parallelised using `Parallel.ForEachAsync` with configurable `MaxDegreeOfParallelism`.
- A typical evaluator runs a full `BacktestRunner` with the chromosome’s genes injected into the strategy, then computes fitness via `IFitnessModel`.
- Fitness values are assigned directly to each chromosome.

### 7.5 Evolution

- **Selection:** Tournament selection (size `TournamentSize`) using the main RNG.
- **Crossover:** Uniform crossover with probability `CrossoverRate`. Genes come from parent1 or parent2.
- **Mutation:** Each gene has a `mutationRate` chance of being randomised. If hyper‑mutation is active, the rate is tripled (capped at 1.0).
- **Elitism:** The best `ElitismPct * PopulationSize` individuals are copied unchanged to the next generation.
- **Stagnation detection:** If the best fitness does not improve for `StagnationGenerationsBeforeHyper` consecutive generations, hyper‑mutation is activated to escape local optima.

### 7.6 State Serialization

`SaveState()` produces a `GeneticOptimizerState` record containing a deep clone of the entire population. `LoadState()` restores it. After loading, call `InvalidateFitness()` if the evaluation data has changed.

---

## 8. Configuration & Specification System

All configuration is represented by immutable `record` types that implement a `Validate()` method. A `ConfigurationException` is thrown for invalid input. There are no silent defaults for critical parameters (Principle 9).

**Specifications:**

- `StrategySpecification` – initial balance, leverage, symbols/timeframes, friction/fitness models.
- `ExecutionSpecification` – date range, warmup, latency, max positions, stop‑out, data policy, parallelism, gene seed.
- `OptimizationSpecification` – master seed, population/generations, mutation/crossover rates, elitism, tournament size, walk‑forward settings, fitness model.
- `LiveSpecification` – magic number, order guard timeout, continuous optimisation settings, notification channels, scheduling.

Each has a static `CreateValidated(...)` factory that performs validation immediately.

**Parsing:** The engine never parses user input (timeframes, etc.) from strings—this is the responsibility of the Cloud. The kernel receives already‑parsed types.

---

## 9. Plugin Loading & Versioning

Plugins (adapters, strategies) are loaded at runtime by the engine using isolated `AssemblyLoadContext`. This is a kernel concern; plugins are never referenced at compile time.

### 9.1 Version Attributes

- `[assembly: ChronosSdkVersion("1.0.0")]` – declares the targeted SDK version.
- `[assembly: AdapterVersion("1.2.3")]` – the adapter’s own version.
- `[AdapterName("MyBroker")]` – discovered by the adapter factory.

### 9.2 Loading Process

1. Scan a configured plugin directory for `.dll` files.
2. For each assembly, load in a new `AssemblyLoadContext` (unloadable later).
3. Check `ChronosSdkVersionAttribute`. Reject if the major version is newer than the engine’s SDK major version, unless an explicit compatibility mode is enabled (future). Older major versions are accepted if they pass interface validation.
4. Discover types with `[AdapterName]` or `IStrategy` implementations.
5. Validate that the type implements the required interfaces (e.g., `IAdapter`, `IStrategy`) and that no missing members exist (interface compatibility).
6. Instantiate the component and register it.

**Isolation:** Each plugin context resolves dependencies independently, avoiding version conflicts. Assemblies must be strongly signed in production; unsigned plugins are rejected.

---

## 10. Messaging & Events

### 10.1 In‑Process Message Bus

`Chronos.Kernel.Messaging.MessageBus` implements `IMessageBus`:

- **Typed subscriptions:** Handlers are stored per message type.
- **Deduplication:** If `message.EventId` is non‑null and has been published within the last 60 seconds, the message is suppressed.
- **Thread safety:** Subscriptions are locked; publishing iterates a snapshot of handlers.

### 10.2 Event Catalog

| Event | Publisher | Payload |
|-------|-----------|---------|
| `BacktestStartedEvent` | BacktestRunner | (timestamp only) |
| `BacktestCompletedEvent` | BacktestRunner | NetProfit, ReturnPct, MaxDrawdown, Sharpe, Sortino, etc. |
| `OrderExecutedEvent` | SimulatedBroker, LiveBroker | Symbol, OrderType, Volume, Price, IsOpen |
| `ConnectionStateEvent` | LiveBroker | IsConnected, AdapterName |
| `LiveReconnectEvent` | LiveBroker | Success, AttemptCount, AdapterName |
| `LiveSessionEndedEvent` | LiveBroker | FinalBalance, FinalEquity, MaxDrawdown, TotalTrades |
| `OptimizationGenerationEvent` | OptimizationRunner (future) | Generation, BestFitness, IsHyperMutation |
| `OptimizationCycleCompletedEvent` | (future) | CycleIndex, BestFitness, GenerationCount |
| `WalkForwardWindowCompletedEvent` | (future) | WindowIndex, date ranges, BestFitness |

---

## 11. Threading & Concurrency

| Component | Threading Model | Notes |
|-----------|-----------------|-------|
| `BacktestRunner` | Single‑threaded | Sync‑over‑async; no concurrency. |
| `SimulatedBroker` | Lock‑protected | All public methods and `OnTickAsync` use `_stateLock`. |
| `LiveBroker` | `SemaphoreSlim(1,1)` | Protects all state. `ExecutionReport` handler also acquires lock. |
| `GeneticOptimizer.EvaluateAsync` | Parallel | Uses `Parallel.ForEachAsync` with configurable max DOP. Chromosomes are evaluated independently. |
| `MessageBus` | Lock‑free for publish | Uses snapshot of handlers; subscriptions locked briefly. |
| Adapter implementations | Must be thread‑safe (Principle 13) | The engine may call adapter methods from multiple threads simultaneously (tick events, timers, command execution). |

**Order of operations in live broker:** Incoming ticks acquire the lock, execute the entire processing, then release. This prevents concurrent state corruption. The periodic sync also acquires the lock, ensuring consistent view.

---

## 12. Telemetry & Observability

`ChronosMetrics` (static class, to become instance‑based in v3.0.0) provides OpenTelemetry metrics via `System.Diagnostics.Metrics`.

| Metric | Instrument | Description |
|--------|------------|-------------|
| `chronos.ga.fitness_improvement` | Histogram | Improvement in best fitness per generation (double) |
| `chronos.backtest.ticks_per_second` | Histogram | Tick processing rate |
| `chronos.live.order_latency_ms` | Histogram | Order placement latency in milliseconds |
| `chronos.live.order_rejections_total` | Counter | Total order rejections |
| `chronos.optimization.duration_seconds` | Histogram | Total optimisation run duration |
| `chronos.live.tick_latency_ticks` | Histogram | Live tick arrival latency (wall clock – tick timestamp) |
| `chronos.live.connection_state` | Gauge | 1 if connected, 0 if disconnected, with `adapter` tag |

All metrics are registered in a `Meter` named `"Chronos.Metrics"`.

---

## 13. Determinism Infrastructure

### 13.1 Random Number Generation

`ChronosRandom` is a custom xorshift128+ implementation that guarantees identical sequences across .NET versions and platforms. It is used for:

- GA population initialisation
- GA selection, crossover, and mutation
- Deterministic gene initialisation for backtests
- Synthetic tick generation (optional)

`System.Random` is never used in any path that affects backtest output or optimisation results.

### 13.2 Seeding Strategy

- A **master seed** is provided by the user (through configuration). All randomness derives from this seed.
- Per‑individual GA seeds are generated with `(masterSeed * 397) ^ index` — stable across .NET versions.
- Backtest gene seeds are generated similarly from the `GeneInitializationSeed` in `ExecutionSpecification`.

### 13.3 System Clock Prohibition

No trading logic accesses `DateTime.UtcNow` or `Environment.TickCount64`. The only exceptions are the `SystemClock` used for order guards, telemetry, and logging, all of which are non‑trading concerns.

### 13.4 Golden Tests

A separate test suite (CI gate) executes a full backtest twice with identical inputs and compares the hash of the serialised trade history. Any difference fails the build. This validates determinism across code changes and .NET updates.

---

## 14. Future Projects (Out of Scope for v1.0.0 LTS)

### 14.1 Chronos.Engine

- Replaces `Hosts.Console`.
- Headless executable.
- Manages plugin loading, connects to Chronos Cloud via WebSocket.
- Receives commands and dispatches them to the kernel.
- Handles encryption, heartbeat, remote updates, binary integrity checks.
- Will be obfuscated and protected against reverse engineering.

### 14.2 Chronos.Cloud

- SaaS web application.
- Sends commands, receives progress/results, stores all data.
- Provides dashboards for monitoring, reporting, and configuration.
- Manages user accounts, licenses, plugin deployment.

Both will be private repositories, developed after the stable v1.0.0 kernel release.
