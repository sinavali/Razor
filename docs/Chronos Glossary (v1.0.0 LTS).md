## Chronos Glossary

**Version:** 1.0.0 LTS  
**Audience:** All users (developers, traders, IT)  
**Status:** Authoritative  
**Last Updated:** 2026-05-19  

---

### Purpose

This glossary defines the domain‑specific terms used throughout the Chronos documentation, codebase, and user interfaces. Use it as a quick reference when you encounter an unfamiliar word or acronym.

Terms are organised alphabetically.

---

### A

**Adapter**  
A plugin DLL that connects Chronos to a specific broker or exchange. An adapter implements `IAdapter`, supplying historical data, live price streaming, and order execution. Examples: MetaTrader 5 adapter, Binance adapter.

**Agnosticism (Market/Exchange/Asset)**  
The principle that Chronos’s core engine contains no knowledge of any particular market type. All exchange‑specific logic resides in adapters.

**AOT (Ahead‑of‑Time Compilation)**  
A .NET compilation mode that produces native code before runtime. Chronos does not use AOT because it relies on reflection for plugin loading and gene injection.

**Asset Class**  
A category of financial instrument: Forex, Crypto Spot, Crypto Perpetual, Equity, Future, CFD.

**Ask**  
The price at which a seller is willing to sell. In a tick, `Ask` is the lowest offer price.

**Async/Await**  
C# keywords for asynchronous programming. Adapter methods are async (network I/O), but strategy `OnTickAsync` should remain synchronous in backtests for determinism.

---

### B

**Backtest**  
A simulation of trading over historical tick data to evaluate a strategy’s past performance.

**Bar (OHLCV)**  
Open, High, Low, Close, Volume data for a fixed timeframe (e.g., 1‑hour candle). Chronos is tick‑only; bars are only used in adapters for data conversion.

**Bid**  
The price at which a buyer is willing to buy. In a tick, `Bid` is the highest bid price.

**Binary Tick File**  
A file format (`.chrs`) used by adapters to store historical tick data. Starts with a header (`CHRS` magic, version 1) followed by raw `Tick` structs.

**Blittable**  
A data type that has an identical memory layout in managed and unmanaged code. `Tick` is blittable, allowing direct memory‑mapped I/O.

**BorrowedTickData**  
A disposable wrapper that groups memory‑mapped tick streams and their file paths. On disposal, it notifies the adapter that the files may be safely deleted.

**Broker**  
In Chronos, an abstraction of a trading account. `IBroker` is implemented by `SimulatedBroker` (for backtesting) and `LiveBroker` (for real trading).

---

### C

**Calmar Ratio**  
A risk‑adjusted return metric: annualised return divided by maximum drawdown.

**Chromosome**  
A candidate solution in the genetic algorithm. A flat array of doubles representing strategy genes and neural network weights.

**ChronosRandom**  
A portable deterministic pseudo‑random number generator (xorshift128+). Guarantees identical sequences across .NET versions and operating systems.

**Chronos Cloud**  
The SaaS web application that manages Chronos Engine instances, stores configurations, runs reports, and provides a dashboard for monitoring.

**Chronos Engine**  
The closed‑source, headless executable that runs on the user’s server. It executes backtests, optimisations, and live trading.

**Chronos Kernel**  
The closed‑source core library containing all trading logic, brokers, GA, and telemetry.

**Chronos Abstractions**  
The public NuGet SDK that plugin developers reference. Contains only interfaces, records, and utilities.

**CI (Continuous Integration)**  
Automated build and test pipeline that verifies code quality and determinism.

**Connection State Event**  
A message published when the live broker’s connection to the exchange changes (connected/disconnected).

**Configuration Exception**  
Exception thrown when an immutable specification record fails validation.

**Cross Margin**  
A margin mode where all open positions share the same margin pool.

**Crossover**  
A GA operation where two parent chromosomes exchange genes to create offspring.

---

### D

**Data Action Policy**  
Enum that defines whether binary tick files are kept, deleted, or cached after use. Values: `KeepUntilExit`, `DeleteAfterTask`, `PersistentCache`.

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
An event from an adapter indicating a change in order state (filled, cancelled, etc.).

**Execution Specification**  
Immutable configuration record for a backtest or optimisation run: date range, latency, warm‑up, etc.

---

### F

**Feature‑Based Versioning**  
A future mechanism where plugins declare supported features via attributes, enabling the host to negotiate compatibility.

**FeedForward Network**  
A type of artificial neural network where connections flow only forward (no cycles). Implemented by `FeedForwardNetwork` in the SDK.

**Fitness**  
A scalar score (higher = better) produced by `IFitnessModel` that rates a backtest result.

**Friction Model**  
An `ISimulationFriction` implementation that calculates slippage and commission for backtesting.

---

### G

**Gene**  
A single optimizable value within a strategy, marked with `[Gene]` attribute. Can be continuous, discrete, categorical, etc.

**Genetic Algorithm (GA)**  
A population‑based optimisation method inspired by evolution. Used to find optimal strategy parameters.

**Golden Test**  
A determinism verification test that runs a backtest twice and compares the hash of the trade history. A CI gate.

---

### H

**Headless**  
Describes the Chronos Engine, which has no graphical user interface; it runs as a console/daemon process and is managed remotely.

**Health Check**  
A periodic verification of engine connectivity, tick freshness, and resource usage. Reported to Chronos Cloud.

**Hyper‑Mutation**  
An elevated mutation rate activated when the GA’s best fitness stagnates for several generations.

---

### I

**IClock**  
Abstraction for time. `TickClock` provides market time; `SystemClock` provides wall‑clock time.

**IMarketCalculator**  
Adapter‑provided interface for exchange‑specific math (margin, PnL, commission, funding, order triggering).

**IMessageBus**  
In‑process publish/subscribe messaging system for domain events.

**Indicator**  
A technical analysis tool that computes values from a tick stream (e.g., SMA, RSI). Derives from `Indicator` base class.

**Isolated Margin**  
A margin mode where each position has its own separate margin allocation.

---

### L

**Latency Ticks**  
A simulated execution delay in the simulated broker, expressed in 100‑ns tick units. 0 = instant execution.

**Leverage**  
The ratio of borrowed funds to margin. e.g., 100:1 leverage means a $1,000 margin controls a $100,000 position.

**Live Specification**  
Immutable configuration for live trading: magic number, continuous optimisation settings, notification channels.

**LTS (Long‑Term Support)**  
Each major Chronos version (1.x, 2.x, …) is an LTS release with guaranteed stability and back‑compatibility within its major.

---

### M

**Magic Number**  
A unique integer that tags all orders from a particular strategy instance. Prevents interference between strategies.

**Margin**  
The amount of capital required to open and maintain a leveraged position. **Free Margin** = Equity – Margin Used.

**Memory‑Mapped File**  
A file whose contents are mapped directly into virtual memory, allowing zero‑copy access. Used for large historical tick files.

**Message Bus**  
See `IMessageBus`.

**Metrics**  
Performance statistics: Net Profit, Return%, Win Rate, Sharpe Ratio, Sortino Ratio, Profit Factor, Calmar Ratio.

---

### N

**Neural Network**  
A machine learning model used within strategies for pattern recognition. Weights can be optimized by the GA.

**Notification Channel**  
A plugin interface (`INotificationChannel`) for sending alerts (email, Telegram, etc.).

---

### O

**Observability**  
The ability to monitor the internal state of the engine through metrics, logs, and health checks.

**OHLCV**  
Open, High, Low, Close, Volume. Bar data; Chronos computes OHLC on‑demand from ticks via `TickWindow`.

**OpenTelemetry**  
A vendor‑neutral observability framework. Chronos emits metrics via `System.Diagnostics.Metrics`.

**Optimisation Specification**  
Immutable GA configuration: generations, population size, mutation rates, walk‑forward windows, etc.

**Order**  
A pending buy/sell request that has not yet triggered. Represented by `Order` record.

**Order Type**  
Enum: `Buy`, `Sell`, `BuyLimit`, `SellLimit`, `BuyStop`, `SellStop`.

---

### P

**Pending Order**  
A limit or stop order that will trigger when the market reaches a specified price.

**Pending Order Trigger Mode**  
How an exchange decides when a pending order is activated: `UseBidForBuy`, `UseAskForBuy`, `UseMidPrice`.

**Plugin**  
A .NET DLL that implements Chronos SDK interfaces. Loaded at runtime by the engine.

**PnL (Profit and Loss)**  
The monetary gain or loss of a position.

**Population**  
The set of all chromosomes in a GA generation.

**Position**  
An open or closed trade, represented by an immutable `Position` record.

**Price Type**  
Which price to use for OHLC aggregation: `Bid`, `Ask`, or `Mid`.

**Principle**  
An immutable design rule in `ChronosPrinciples.md`. Non‑negotiable; violations block merges.

**Profit Factor**  
Gross profit divided by gross loss. > 1 indicates profitability.

---

### R

**Reconciliation**  
The process of synchronising the live broker’s local state with the exchange’s actual account state.

**ReLU (Rectified Linear Unit)**  
An activation function: `max(0, x)`.

---

### S

**SaaS**  
Software as a Service. Chronos Cloud is a SaaS product; the engine is on‑premise.

**SDK (Software Development Kit)**  
The `Chronos.Abstractions` NuGet package that plugin developers use.

**Seed**  
A number used to initialise a random number generator. Chronos derives all randomness from a master seed.

**SemVer (Semantic Versioning)**  
Versioning scheme: `Major.Minor.Patch`. Chronos follows SemVer with LTS per major version.

**Sharpe Ratio**  
A risk‑adjusted return metric: average return above risk‑free rate divided by standard deviation of returns.

**Sigmoid**  
An S‑shaped activation function: `1 / (1 + e^(-x))`.

**Slippage**  
The difference between the expected price of a trade and the price at which it is actually executed.

**Sortino Ratio**  
Like Sharpe ratio but only considers downside volatility.

**Stagnation**  
A GA condition where the best fitness does not improve for several generations. Triggers hyper‑mutation.

**Stop‑Loss (SL)**  
A price level at which a losing position is automatically closed.

**Stop‑Out Level**  
The margin level ratio (e.g., 0.5 = 50%) at which the broker force‑closes the worst position.

**Strategy**  
A plugin that implements `IStrategy` or `StrategyBase`, containing the trading logic.

**Strategy Specification**  
Immutable configuration: initial balance, leverage, symbols/timeframes, friction/fitness models.

**Swap**  
Overnight interest charged or earned for holding a position. Also called rollover or funding.

**Symbol Properties**  
Exchange‑specific metadata for a trading symbol (tick size, contract size, margin rates, etc.).

---

### T

**Tanh (Hyperbolic Tangent)**  
An activation function: `tanh(x)`, output range `[-1, 1]`.

**Tick**  
A single price update: timestamp, bid, ask, volume. The fundamental data unit in Chronos.

**Tick Clock**  
An `IClock` implementation driven by tick timestamps. Used for all trading calculations.

**Tick Window**  
A sliding ring buffer of recent ticks per symbol. Provides on‑demand OHLC aggregation.

**Tick‑Only Core**  
The principle that Chronos never processes bars natively; all operations use raw ticks.

**TimeFrame**  
An enum representing aggregation periods: `Tick`, `M1`, `M5`, `H1`, `D1`, etc.

**Tournament Selection**  
A GA selection method where a random group of chromosomes is chosen and the best is selected.

---

### V

**Validation**  
Every configuration record must implement `Validate()` and throw `ConfigurationException` on invalid input.

**Version Manager**  
The Chronos subsystem that inspects plugin SDK version attributes and decides whether to load them.

---

### W

**Walk‑Forward Analysis**  
An optimisation technique that splits data into multiple training/testing windows to avoid overfitting.

**Wall‑Clock Time**  
Real‑world time, as opposed to tick‑driven market time. Used only for scheduling, telemetry, and health checks.

**Warm‑up**  
An initial period of backtest data where signals are computed but no trades are placed, used to prime indicators.

---

### X

**xorshift128+**  
The algorithm used by `ChronosRandom` for portable, deterministic random number generation.

---

*This glossary is maintained alongside the Chronos documentation. Add new terms when they are introduced.*