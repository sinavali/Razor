# Chronos.Core.Kernel

**Version:** 1.0.0 LTS  
**Status:** Internal – closed source  
**Last Updated:** 2026-07-09  

---

## Overview

`Chronos.Core.Kernel` is the closed‑source heart of the Chronos trading engine. It contains all trading logic, including backtesting, live trading, genetic optimisation, brokers, hook infrastructure, telemetry, and messaging. This project is the **core runtime** that executes strategies, evaluates fitness, and manages account state.

This project is **not** part of the public SDK. It is referenced by `Chronos.Core.Engine` and is never exposed directly to extension developers.

---

## Key Components

### 1. Backtesting (`Chronos.Core.Kernel.Backtesting`)

The backtesting engine processes pre‑loaded tick data and simulates strategy execution deterministically.

| Component | Description |
|-----------|-------------|
| `BacktestInput` | Immutable record containing tick streams, strategy, specifications, genes, and hooks. |
| `BacktestRunner` | Orchestrates tick‑by‑tick execution. Implements `IBacktestRunner`. |
| `BacktestResult` | Immutable result record with final balance, equity, drawdown, and trade history. |
| `MergedTickTimeline` | Merges multiple tick streams into a single chronological enumerator. |

**Execution flow:**
1. Create `TickClock` and `SimulatedBroker`.
2. Inject genes into the strategy.
3. Call `OnConfigureAsync()` and `OnStartAsync()`.
4. Process each tick through the merged timeline:
   - Update `TickClock`.
   - Invoke `backtest.tick.received` filter hooks.
   - Call `broker.OnTickAsync()`.
   - Invoke `backtest.tick.strategy_before` filter hooks.
   - Push tick to `TickWindow`.
   - Call `strategy.OnTick()`.
   - Invoke action hooks (`backtest.tick.strategy_after`, `backtest.tick.completed`).
5. Close all positions, dispose indicators, call `OnStopAsync()`.
6. Assemble `BacktestResult` and publish `BacktestCompletedEvent`.

---

### 2. Brokers (`Chronos.Core.Kernel.Brokers`)

Two broker implementations provide live‑backtest parity through a shared `IMarketCalculator`.

#### SimulatedBroker

- Deterministic, single‑threaded broker for backtesting and optimisation.
- **State:** Lock‑protected mutable internal positions.
- **Order lifecycle:** Market orders (instant or latency‑delayed), pending orders (limit/stop triggered by price).
- **SL/TP:** Evaluated on each tick for the symbol.
- **Stop‑out:** Force‑closes the worst position when `Equity / MarginUsed ≤ StopOutLevel`.
- **Holding costs:** Daily swap/funding charged based on `TickClock` time.
- **Warm‑up:** When `IsWarmup = true`, all orders are rejected.

#### LiveBroker

- Wraps an `IAdapterCapability` for real exchange trading.
- **State:** `SemaphoreSlim` ensures thread‑safe access.
- **Tick handling:** `ProcessTickAsync()` updates prices, PnL, SL/TP, and triggers stop‑out.
- **Reconciliation:** `ReconcileAsync()` fetches full account state from the adapter.
- **In‑flight order guard:** Uses monotonic clock to prevent duplicate orders.
- **Execution reports:** Handled asynchronously with full exception logging.
- **Connection management:** `ConnectAndNotifyAsync()` and `DisconnectAndNotifyAsync()`.

---

### 3. Clock System (`Chronos.Core.Kernel.Clock`)

Two `IClock` implementations enforce separation of market time and wall‑clock time.

| Clock | Purpose | Used By |
|-------|---------|---------|
| `TickClock` | Market time driven by tick timestamp | All trading calculations |
| `SystemClock` | Wall‑clock time for non‑trading concerns | Order guards, telemetry, logging |

**Enforcement:** Any call to `DateTime.UtcNow` inside trading paths is a build‑breaking violation.

---

### 4. Configuration (`Chronos.Core.Kernel.Configuration`)

Immutable specification records with strict validation.

| Specification | Description |
|---------------|-------------|
| `ExecutionSpecification` | Backtest parameters: date range, latency, warmup, max positions, stop‑out, parallelism, gene seed. |
| `OptimizationSpecification` | GA parameters: master seed, generations, population, mutation/crossover rates, elitism, tournament size. |
| `LiveSpecification` | Live trading: magic number, order guard timeout. |

All specifications implement `Validate()` and throw `ConfigurationException` on invalid input.

---

### 5. Events (`Chronos.Core.Kernel.Events`)

Domain events published via the in‑process message bus.

| Event | Publisher | Key Fields |
|-------|-----------|------------|
| `BacktestStartedEvent` | BacktestRunner | Timestamp |
| `BacktestCompletedEvent` | BacktestRunner | NetProfit, ReturnPct, MaxDrawdown, Sharpe, Sortino |
| `OrderExecutedEvent` | SimulatedBroker, LiveBroker | Symbol, OrderType, Volume, Price, IsOpen |
| `ConnectionStateEvent` | LiveBroker | IsConnected, AdapterName |
| `LiveReconnectEvent` | LiveBroker | Success, AttemptCount |
| `LiveSessionEndedEvent` | LiveBroker | FinalBalance, FinalEquity, MaxDrawdown, TotalTrades |
| `OptimizationGenerationEvent` | OptimizationRunner | Generation, BestFitness, IsHyperMutation |
| `OptimizationCycleCompletedEvent` | OptimizationRunner | CycleIndex, BestFitness, GenerationCount |

---

### 6. Hook System (`Chronos.Core.Kernel.Hooks`)

The priority‑based extensibility mechanism that powers all plugin functionality.

**Components:**
- `HookRegistry` – Root registry with `Backtest`, `Live`, `Optimization`, `Report` sub‑registries.
- `HookInvoker` – Static helper for invoking filter and action chains.
- `FilterRegistration<T>` / `ActionRegistration<T>` – Default implementations of registration interfaces.

**Hook contexts:**
- `BacktestContext` – Current tick, equity, balance, drawdown, broker, tick window, open positions.
- `LiveContext` – Current tick, equity, balance, drawdown, broker, adapter name, connection state.
- `OptimizationContext` – Current generation, total generations, best fitness, hyper‑mutation status.
- `ReportContext` – Report format.

**Invocation order:** Priority → Plugin name → Registration order.

---

### 7. Indicator Registry (`Chronos.Core.Kernel.Indicators`)

Manages indicator creation, caching, and lifecycle.

- `IndicatorRegistryFactory.Create(TickWindow)` – Creates a new registry instance.
- `IndicatorRegistry.Get<T>(params object[] args)` – Caches indicators by type and constructor arguments.
- **Reference counting:** Indicators are kept alive while referenced by strategies.
- **Disposal:** `DisposeAll()` releases all rented array buffers.

---

### 8. Messaging (`Chronos.Core.Kernel.Messaging`)

In‑process publish/subscribe system.

- `IMessageBus` – Interface for publishing and subscribing to typed messages.
- `MessageBus` – Default implementation with:
  - **Deduplication:** EventId‑based suppression for 60 seconds.
  - **Thread safety:** Lock‑free publishing with snapshot of handlers.
  - **Cleanup:** Periodic removal of dead weak references.

---

### 9. Metrics (`Chronos.Core.Kernel.Metrics`)

Performance metric calculation.

| Component | Description |
|-----------|-------------|
| `IMetricsCalculator` | Interface for calculating performance metrics. |
| `MetricsCalculator` | Computes Sharpe, Sortino, Calmar, Profit Factor, Win Rate. |
| `SummaryMetrics` | Immutable record containing all key metrics. |
| `FitnessCalculator` | Fallback fitness calculator (net profit). |

---

### 10. Neural Networks (`Chronos.Core.Kernel.NeuralNetworks`)

Built‑in neural network implementation.

- `FeedForwardNetwork` – Configurable feed‑forward network with ReLU, Tanh, or Sigmoid activation.
- **Initialisation:** Xavier initialisation for weights and biases.
- **GA integration:** Implements `INeuralNetworkModel` with `ParameterCount`, `LoadParameters()`, `ExportParameters()`.
- **Serialisation:** `SerializeState()` and `DeserializeState()` for pause/resume.

---

### 11. Optimisation (`Chronos.Core.Kernel.Optimization`)

Genetic algorithm implementation.

| Component | Description |
|-----------|-------------|
| `Chromosome` | Candidate solution with `double[] Genes` and `Fitness` value. |
| `GeneticOptimizer` | Steppable GA with elitism, tournament selection, crossover, mutation, hyper‑mutation. |
| `GeneticOptimizerState` | Serializable snapshot for pause/resume. |
| `OptimizationRunner` | Orchestrates the full optimisation run, including fitness evaluation via hooks. |

**GA parameters:**
- Population size, generations, mutation rate, crossover rate, elitism percentage, tournament size.
- Stagnation detection and hyper‑mutation (3× base mutation rate).
- Parallel evaluation with configurable `MaxDegreeOfParallelism`.

**Fitness evaluation:** Handled via the `optimization.fitness.evaluate` action hook. Plugins set `context.Fitness`; the first non‑NaN value wins.

---

### 12. Reporting (`Chronos.Core.Kernel.Reporting`)

**Note:** The engine does **not** generate formatted reports. The `ReportGenerator` class exists solely to invoke `report.before_generate` and `report.after_generate` hooks. Report rendering is the responsibility of Chronos Cloud.

---

### 13. Telemetry (`Chronos.Core.Kernel.Telemetry`)

OpenTelemetry metrics via `System.Diagnostics.Metrics`.

| Metric | Instrument | Description |
|--------|------------|-------------|
| `core.ga.fitness_improvement` | Histogram | Improvement in best fitness per generation. |
| `core.backtest.ticks_per_second` | Histogram | Tick processing rate. |
| `core.live.order_latency_ms` | Histogram | Order placement latency in milliseconds. |
| `core.live.order_rejections_total` | Counter | Total order rejections. |
| `core.optimization.duration_seconds` | Histogram | Total optimisation run duration. |
| `core.live.tick_latency_ticks` | Histogram | Live tick arrival latency (wall clock – tick timestamp). |
| `core.live.connection_state` | Gauge | 1 if connected, 0 if disconnected (with `instance_id` tag). |

**Instance‑based:** Each `CoreMetrics` instance is tagged with an `instance_id`, enabling multi‑engine deployments.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Chronos.Core.Sdk` | Provides all public contracts, domain types, and helpers. |
| `Chronos.Core.Shared` | Provides memory‑mapped tick lists and binary file mapping. |
| `Microsoft.Extensions.Logging.Abstractions` | Logging interfaces. |
| `MessagePack` | Serialisation for optimisation state. |

---

## Build & Integration

This project is compiled as a `net10.0` class library and is referenced by:

- `Chronos.Core.Engine` (via `KernelService` facade)
- `Chronos.Core.Kernel.UnitTests` and `Chronos.Core.Kernel.IntegrationTests`

It has **no public API** – all types are internal to the `Chronos.Core` namespace, except those explicitly exposed via `InternalsVisibleTo`.

---

## Testing

Unit and integration tests verify:

- **Determinism:** Golden tests ensure bit‑identical outputs across runs.
- **Broker parity:** Simulated vs live broker output comparison for identical tick sequences.
- **GA correctness:** Population evolution, fitness evaluation, and state serialisation.
- **Hook invocation:** Filter and action chain execution order and exception handling.

---

## Performance Considerations

- **Tick processing:** Single‑threaded, allocation‑free where possible.
- **Memory:** `TickWindow` and `Indicator` use pooled arrays (`ArrayPool`).
- **Parallelism:** `GeneticOptimizer.EvaluateAsync()` uses `Parallel.ForEachAsync` with configurable max DOP.
- **File I/O:** Historical tick data is memory‑mapped (zero‑copy read).

---

## Future Enhancements

- **NEAT (NeuroEvolution of Augmenting Topologies):** Support for evolving network structure.
- **Island Model GA:** Multiple isolated populations with migration.
- **Adaptive mutation rates:** 1/5th rule for dynamic mutation scaling.

---

*This README is intended for Chronos core developers. For extension development, see the [Chronos.Core.Sdk README](src/Chronos.Core.Sdk/README.md).*