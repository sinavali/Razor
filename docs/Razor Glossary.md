# Razor Glossary

**Version:** 1.0.0 LTS  
**Audience:** All users (developers, traders, IT)  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

## Purpose

This glossary defines the domain‑specific terms used throughout the Razor documentation, codebase, and user interfaces. Use it as a quick reference when you encounter an unfamiliar word or acronym.

Terms are organised alphabetically.

---

### A

**Action Hook**
A type of hook that observes an event but cannot modify or reject data. The callback receives event data and a hook context. Registered via `IActionRegistration<T>`.

**Adapter**
An extension DLL that connects Razor to a specific broker or exchange. An adapter implements `IAdapterCapability` (in `Razor.Core.Sdk.Slots.Adapter`), supplying historical data, live price streaming, and order execution. Examples: MetaTrader 5 adapter, Binance adapter.

**AdapterNameAttribute**
An attribute used to declare a human‑readable name for an adapter implementation. The engine uses this attribute to discover adapters by name.

**Agnosticism (Market/Exchange/Asset)**
The principle that Razor's core engine contains no knowledge of any particular market type. All exchange‑specific logic resides in adapters.

**AOT (Ahead‑of‑Time Compilation)**
A .NET compilation mode that produces native code before runtime. Razor does not use AOT because it relies on reflection for extension loading and gene injection.

**Asset Class**
A category of financial instrument: `Forex`, `CryptoSpot`, `CryptoPerpetual`, `Equity`, `Future`, `CFD`.

**Ask**
The price at which a seller is willing to sell. In a tick, `Ask` is the lowest offer price.

---

### B

**Backtest**
A simulation of trading over historical tick data to evaluate a strategy's past performance.

**BacktestInput**
An immutable record containing all data and configuration required to run a backtest: tick streams, symbols, strategy instance, specifications, and optional genes.

**BacktestRunner**
The orchestrator class that executes a backtest, processing ticks sequentially through the broker and strategy.

**Bar (OHLCV)**
Open, High, Low, Close, Volume data for a fixed timeframe (e.g., 1‑hour candle). Razor is tick‑only; bars are only used in adapters for data conversion.

**BehaviorRecorder**
A service that records sparse behavioural data (state, action, reward) from strategies for reinforcement learning training. Records are buffered, compressed, and uploaded to Cloud.

**Bid**
The price at which a buyer is willing to buy. In a tick, `Bid` is the highest bid price.

**Binary Tick File**
A file format (`.chrs`) used by adapters to store historical tick data. Starts with a header (`CHRS` magic, version 1) followed by raw `Tick` structs.

**Blittable**
A data type that has an identical memory layout in managed and unmanaged code. `Tick` is blittable, allowing direct memory‑mapped I/O.

**BorrowedTickData**
A disposable wrapper that groups memory‑mapped tick streams and their file paths. On disposal, it notifies the adapter that the files may be safely deleted.

**Broker**
In Razor, an abstraction of a trading account. `IBroker` (in `Razor.Core.Sdk.Shared`) is implemented by `SimulatedBroker` (for backtesting) and `LiveBroker` (for real trading).

**Bundle**
A single extension DLL that contains multiple components — e.g., an adapter, a strategy, several indicators, and hook registrations — all in one assembly.

---

### C

**Calmar Ratio**
A risk‑adjusted return metric: annualised return divided by maximum drawdown.

**Capability Interface**
An interface in the `Razor.Core.Sdk.Slots` namespace that a slot extension must implement. The three capability interfaces are `IAdapterCapability`, `IStrategyCapability`, and `INeuralNetworkModel`.

**Chromosome**
A candidate solution in the genetic algorithm. A flat array of doubles representing strategy genes and neural network parameters.

**Razor Cloud**
The SaaS web application that manages Razor Engine instances, stores configurations, runs reports, and provides a dashboard for monitoring.

**Razor Engine**
The closed‑source, headless executable that runs on the user's server. It executes backtests, optimisations, and live trading.

**Razor Kernel**
The closed‑source core library containing all trading logic, brokers, GA, hook invoker, and telemetry.

**Razor.Core.Sdk**
The public NuGet SDK that extension developers reference. Contains only hooks, slots, domain types, and utilities — no runtime logic.

**CI (Continuous Integration)**
Automated build and test pipeline that verifies code quality and determinism.

**Configuration Exception**
Exception thrown when an immutable specification record fails validation.

**Cross Margin**
A margin mode where all open positions share the same margin pool.

**Crossover**
A GA operation where two parent chromosomes exchange genes to create offspring.

**CustomizedRandom**
A portable deterministic pseudo‑random number generator (xorshift128+). Guarantees identical sequences across .NET versions and operating systems. Located in `Razor.Core.Sdk.Shared`.

---

### D

**Data Action Policy**
Enum (`DataActionPolicy` in `Razor.Core.Sdk.Shared`) that defines whether binary tick files are kept, deleted, or cached after use. Values: `KeepUntilExit`, `DeleteAfterTask`, `PersistentCache`.

**Determinism**
The guarantee that identical inputs produce bit‑identical outputs every run, on any supported platform.

**Drawdown**
The percentage decline from a peak in equity. **Daily Drawdown** resets at the start of each UTC day.

---

### E

**Elitism**
A GA strategy where the best chromosomes are copied unchanged to the next generation.

**Equity**
Current account balance plus floating (unrealised) profit/loss.

**Execution Report**
A record (`ExecutionReport` in `Razor.Core.Sdk.Shared`) from an adapter indicating a change in order state (filled, cancelled, etc.).

**Execution Specification**
Immutable configuration record for a backtest or optimisation run: date range, latency, warm‑up, etc. Located in `Razor.Core.Kernel.Configuration`.

**Extension**
A .NET DLL that implements one or more contracts from `Razor.Core.Sdk`. May be a slot (Adapter, Strategy, NN Model), an Indicator, a Hook Plugin, or any combination thereof. Loaded at runtime by the engine.

**Extension Manifest**
The list of all discovered extensions (adapters, strategies, indicators, hook plugins, NN models) that the engine sends to Razor Cloud after scanning its directories. The Cloud uses this to present activation options to the user.

---

### F

**Feature Attribute**
An attribute used to mark an interface or class with a specific capability version requirement for feature negotiation.

**Filter Hook**
A type of hook that transforms or rejects data as it flows through the pipeline. The callback returns a `FilterResult<T>` indicating whether to allow (possibly modified) or reject the data. Registered via `IFilterRegistration<T>`.

**FilterResult**
A struct (`FilterResult<T>` in `Razor.Core.Sdk.Hooks`) representing the result of a filter hook. Created via the static factory class `FilterResult`.

**Fitness**
A scalar score (higher = better) that rates a backtest result. Fitness evaluation is performed by hook plugins via the `optimization.chromosome.evaluated` action hook, not by a built‑in `IFitnessModel`.

---

### G

**Gene**
A single optimisable value within a strategy, marked with `[Gene]` attribute. Can be continuous, discrete, categorical, structural, or parametric.

**GeneInjector**
Static helper class in `Razor.Core.Sdk.Shared` that extracts gene schemas, injects gene values into strategy properties and neural network models, and builds chromosome arrays.

**Genetic Algorithm (GA)**
A population‑based optimisation method inspired by evolution. Used to find optimal strategy parameters.

**GeneticOptimizerState**
An immutable snapshot of the optimiser's population and parameters, allowing pause/resume of long‑running optimisations.

**Golden Test**
A determinism verification test that runs a backtest twice and compares the hash of the trade history. A CI gate.

---

### H

**Headless**
Describes the Razor Engine, which has no graphical user interface; it runs as a console/daemon process and is managed remotely.

**Health Check**
A periodic verification of engine connectivity, tick freshness, and resource usage. Reported to Razor Cloud.

**Hook**
A named point in the engine's pipeline where extensions can register callbacks. Hooks are either filters (can modify/reject data) or actions (observe only). All hook registration interfaces are in `Razor.Core.Sdk.Hooks`.

**Hook Context**
An interface (`IHookContext` and its specialised descendants) passed to every hook callback, providing context data such as the hook name, UTC time, cancellation token, and pipeline‑specific state.

**Hook Manifest**
An interface (`IHookManifest`) implemented by hook plugin DLLs. The engine calls `RegisterHooks(IHookRegistry)` to allow the plugin to register its callbacks.

**Hook Registry**
The root registry (`IHookRegistry`) with four sub‑registries: `Backtest`, `Live`, `Optimization`, and `Report`. Each exposes typed hook registration points.

**Hyper‑Mutation**
An elevated mutation rate activated when the GA's best fitness stagnates for several generations.

---

### I

**IClock**
Abstraction for time. `TickClock` provides market time; `SystemClock` provides wall‑clock time.

**IMarketCalculator**
Adapter‑provided interface for exchange‑specific math (margin, PnL, commission, funding, order triggering). Located in `Razor.Core.Sdk.Shared`.

**IMessageBus**
In‑process publish/subscribe messaging system for domain events. Located in `Razor.Core.Kernel.Messaging` (internal, not in the public SDK).

**Indicator**
A technical analysis tool that computes values from a tick stream (e.g., SMA, RSI). Derives from the `Indicator` base class in `Razor.Core.Sdk.Shared`.

**IWindowAwareIndicator**
Interface in `Razor.Core.Sdk.Shared` that indicators implement to receive the `TickWindow` dependency.

**IRegistryAwareIndicator**
Interface in `Razor.Core.Sdk.Shared` that indicators implement to receive the `IIndicatorRegistry` for cross‑indicator references.

**Isolated Margin**
A margin mode where each position has its own separate margin allocation.

---

### L

**Latency Ticks**
A simulated execution delay in the simulated broker, expressed in 100‑ns tick units. 0 = instant execution.

**Leverage**
The ratio of borrowed funds to margin. e.g., 100:1 leverage means a $1,000 margin controls a $100,000 position.

**Live Specification**
Immutable configuration for live trading: magic number and order guard timeout. Continuous optimisation fields have been removed (Cloud‑orchestrated). Located in `Razor.Core.Kernel.Configuration`.

**LTS (Long‑Term Support)**
Each major Razor version (1.x, 2.x, …) is an LTS release with guaranteed stability and backward compatibility within its major.

---

### M

**Magic Number**
A unique integer that tags all orders from a particular strategy instance. Prevents interference between strategies.

**Margin**
The amount of capital required to open and maintain a leveraged position. **Free Margin** = Equity – Margin Used.

**Memory‑Mapped File**
A file whose contents are mapped directly into virtual memory, allowing zero‑copy access. Used for large historical tick files via `MemoryMappedTickList`.

**Metrics**
Performance statistics: Net Profit, Return%, Win Rate, Sharpe Ratio, Sortino Ratio, Profit Factor, Calmar Ratio. Custom metrics are computed by hook plugins via the `backtest.completed` and `live.*` hooks.

---

### N

**Neural Network Model**
A slot capability implementing `INeuralNetworkModel` (in `Razor.Core.Sdk.Slots.NeuralNetwork`). Supports feed‑forward, ONNX, LSTM, RL, and other architectures through a unified parameter‑vector interface compatible with the GA.

---

### O

**Observability**
The ability to monitor the internal state of the engine through metrics, logs, and health checks.

**OHLCV**
Open, High, Low, Close, Volume. Bar data; Razor computes OHLC on‑demand from ticks via `TickWindow`.

**OpenTelemetry**
A vendor‑neutral observability framework. Razor emits metrics via `System.Diagnostics.Metrics`.

**Optimisation Specification**
Immutable GA configuration: master seed, generations, population size, mutation/crossover rates, elitism, tournament size. Located in `Razor.Core.Kernel.Configuration`.

**Order**
A pending buy/sell request that has not yet triggered. Represented by the `Order` record in `Razor.Core.Sdk.Shared`.

**Order Type**
Enum (`OrderType` in `Razor.Core.Sdk.Shared`): `Buy`, `Sell`, `BuyLimit`, `SellLimit`, `BuyStop`, `SellStop`.

---

### P

**Pending Order**
A limit or stop order that will trigger when the market reaches a specified price.

**Pending Order Trigger Mode**
Enum (`PendingOrderTriggerMode` in `Razor.Core.Sdk.Shared`): `UseBidForBuy`, `UseAskForBuy`, `UseMidPrice`.

**PnL (Profit and Loss)**
The monetary gain or loss of a position.

**Population**
The set of all chromosomes in a GA generation.

**Position**
An open or closed trade, represented by an immutable `Position` record in `Razor.Core.Sdk.Shared`.

**Price Type**
Enum (`PriceType` in `Razor.Core.Sdk.Shared`) for OHLC aggregation: `Bid`, `Ask`, or `Mid`.

**Principle**
An immutable design rule in `RazorPrinciples.md`. Non‑negotiable; violations block merges.

**Priority**
An integer assigned to a hook registration. Lower numbers execute earlier. Same‑priority hooks are ordered alphabetically by plugin name, then by registration order.

**Profit Factor**
Gross profit divided by gross loss. > 1 indicates profitability.

---

### R

**Reconciliation**
The process of synchronising the live broker's local state with the exchange's actual account state.

**ReLU (Rectified Linear Unit)**
An activation function: `max(0, x)`.

**Report Generator**
A class that invokes the `report.before_generate` and `report.after_generate` hooks. Report rendering is handled by Razor Cloud.

---

### S

**SaaS**
Software as a Service. Razor Cloud is a SaaS product; the engine is on‑premise.

**SDK (Software Development Kit)**
The `Razor.Core.Sdk` NuGet package that extension developers use.

**SdkVersionAttribute**
An assembly‑level attribute (`[assembly: SdkVersion("1.0.0")]`) that every extension must declare to specify the targeted SDK version.

**Seed**
A number used to initialise a random number generator. Razor derives all randomness from a master seed.

**SemVer (Semantic Versioning)**
Versioning scheme: `Major.Minor.Patch`. Razor follows SemVer with LTS per major version.

**Sharpe Ratio**
A risk‑adjusted return metric: average return above risk‑free rate divided by standard deviation of returns.

**Sigmoid**
An S‑shaped activation function: `1 / (1 + e^(-x))`.

**Slippage**
The difference between the expected price of a trade and the price at which it is actually executed. Handled by the adapter's `IMarketCalculator` and order execution logic.

**Slot**
A required capability that the engine needs to operate. The three slots are Adapter, Strategy, and Neural Network Model. Only one Adapter and one Strategy can be active at a time. Activation is managed through Razor Cloud.

**Sortino Ratio**
Like Sharpe ratio but only considers downside volatility.

**Stagnation**
A GA condition where the best fitness does not improve for several generations. Triggers hyper‑mutation.

**Stop‑Loss (SL)**
A price level at which a losing position is automatically closed.

**Stop‑Out Level**
The margin level ratio (e.g., 0.5 = 50%) at which the broker force‑closes the worst position.

**Strategy**
An extension DLL implementing `IStrategyCapability` (in `Razor.Core.Sdk.Slots.Strategy`), containing the trading logic.

**Strategy Specification**
Immutable configuration: initial balance, leverage, symbols/timeframes. Located in `Razor.Core.Sdk.Shared`.

**Swap**
Overnight interest charged or earned for holding a position. Also called rollover or funding.

**Symbol Properties**
Exchange‑specific metadata for a trading symbol (tick size, contract size, margin rates, etc.). Located in `Razor.Core.Sdk.Shared`.

**SystemClock**
An `IClock` implementation providing wall‑clock time for non‑trading purposes (order guards, telemetry, logging).

---

### T

**Tanh (Hyperbolic Tangent)**
An activation function: `tanh(x)`, output range `[-1, 1]`.

**Tick**
A single price update: timestamp, bid, ask, volume. The fundamental data unit in Razor.

**Tick Clock**
An `IClock` implementation driven by tick timestamps. Used for all trading calculations.

**Tick Window**
A sliding ring buffer of recent ticks per symbol. Provides on‑demand OHLC aggregation. Located in `Razor.Core.Sdk.Shared`.

**Tick‑Only Core**
The principle that Razor never processes bars natively; all operations use raw ticks.

**TickSynthesizer**
A static helper class in `Razor.Core.Sdk.Shared` that converts bar data into synthetic tick arrays and computes stream metrics.

**TimeFrame**
An enum in `Razor.Core.Sdk.Shared` representing aggregation periods: `Tick`, `M1`, `M5`, `H1`, `D1`, etc.

**Tournament Selection**
A GA selection method where a random group of chromosomes is chosen and the best is selected.

---

### V

**Validation**
Every configuration record must implement `Validate()` and throw `ConfigurationException` on invalid input.

---

### W

**Walk‑Forward Analysis**
An optimisation technique that splits data into multiple training/testing windows to avoid overfitting. Orchestrated by Razor Cloud using repeated backtest and optimisation commands; not built into the engine.

**Wall‑Clock Time**
Real‑world time, as opposed to tick‑driven market time. Used only for scheduling, telemetry, and health checks.

**Warm‑up**
An initial period of backtest data where signals are computed but no trades are placed, used to prime indicators. Controlled by `WarmupWindowCount` in `ExecutionSpecification`.

---

### X

**xorshift128+**
The algorithm used by `CustomizedRandom` for portable, deterministic random number generation.

---

*This glossary is maintained alongside the codebase. Any new domain term should be added here before appearing in documentation.*