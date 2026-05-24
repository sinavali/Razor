## Merged Feature Roadmap

### 1. Plugin Architecture & Ecosystem
- **Plugin Discovery & Hot‑Swapping** – Load strategy, indicator, risk manager, and neural network DLLs from a directory; discover `IStrategy` etc. via attributes; run in isolated `AssemblyLoadContext` with zero‑downtime swapping.
- **Feature‑Based Versioning** – Plugins declare supported features (e.g. `[ChronosFeature("Margin","1.0")]`); the host negotiates compatibility.
- **Assembly Signing & Verification** – Enforce strong‑name signing for all plugins in production; verify signatures before loading.
- **Marketplace DRM Wrappers** – Code‑signing and time‑bombing utilities for third‑party plugin vendors to prevent piracy.
- **Strategy Scaffolding CLI** – `dotnet new chronos‑strategy` template generator to quickly scaffold a new strategy project.

### 2. Execution & Order Management
- **Smart Order Routing (SOR)** – Meta‑adapter that routes orders across multiple brokers based on best bid/ask.
- **Iceberg / Hidden Orders** – Randomise large orders into smaller market slices using the system clock.
- **TWAP / VWAP Execution Algos** – Built‑in algorithms accessible via `Broker.ExecuteTwapAsync()`.
- **Post‑Only Limit Orders** – Flag orders to guarantee maker rebates, cancelling/re‑pricing if they would cross the spread.
- **Local Order Book SL/TP** – Maintain stop‑loss/take‑profit levels engine‑side and fire market orders when triggered.
- **Cross‑Margin Portfolio Sizing** – Size positions based on total portfolio risk rather than isolated symbol equity.
- **Kill‑Switch Triggers** – Global equity drop thresholds that instantly liquidate and disconnect adapters.
- **OCO (One‑Cancels‑Other) Orders** – Link a stop‑loss and limit order so that filling one cancels the other.
- **Trailing Stops** – Dynamic stop‑loss that moves with the price.
- **Bracket Orders** – Entry, stop‑loss, and take‑profit combined into an atomic group.
- **Multi‑Leg Orders** – Support spread trading, pairs trading, and other multi‑instrument orders.
- **Partial Fills Simulation** – Realistic fill simulation that takes order‑book depth into account.
- **Market Impact Models** – Slippage models that adjust for order size relative to available liquidity.

### 3. Risk & Compliance
- **Value at Risk (VaR) Modelling** – Historical and parametric VaR calculated in real time during live trading.
- **Audit Trail Logging** – Immutable, write‑only SQLite logs of every broker API request for regulatory compliance.
- **Fat‑Finger Limits** – Hard‑coded maximum order sizes that override any strategy request.
- **Margin Call Predictive Alerts** – Notify via Telegram/Email if the current drawdown trajectory will hit stop‑out within one hour.
- **Wash Trading Prevention** – Reject orders that would execute against the strategy’s own resting limit orders.

### 4. Data, Analytics & Research
- **L2/L3 Order Book Replay** – Support `DepthTick` structs for DOM‑based backtesting.
- **Footprint / Volume Profile Generation** – Synthesise volume profiles locally from raw ticks.
- **Synthetic Spread Symbols** – Define virtual symbols (e.g. `EURUSD‑GBPUSD`) that generate synthetic ticks.
- **Walk‑Forward 3D Surface Plots** – Export JSON for Plotly.js to render 3D optimisation landscapes.
- **Trade Correlation Matrix** – Show overlapping exposure times between strategies/symbols after a backtest.
- **Slippage Heatmaps** – Track expected slippage by time‑of‑day and volume.
- **Custom Tick Synthesizer Profiles** – User‑defined tick generation behaviour (random walk vs. historical spread) for legacy bar conversion.
- **Cloud‑Hosted Tick Datalake** – Central repository streaming historical MMF chunks to engines via gRPC.
- **Cloud Storage Adapters** – Read historical data directly from AWS S3, Azure Blob, etc.
- **Multi‑Exchange Data Aggregation** – Composite adapter that merges liquidity from multiple exchanges for a single symbol.
- **Data Quality Checks** – Automatic validation for gaps, spikes, and staleness before backtesting or live use.
- **Tick Data Compression & Streaming** – Compressed binary format; stream from cloud storage without full download.
- **Monte Carlo Simulation** – Randomised backtest variations to assess strategy robustness.
- **Sensitivity Analysis** – Vary individual parameters and measure performance impact.
- **Custom Report Templates** – User‑definable report layouts (HTML, PDF) with charts and statistics.

### 5. Machine Learning & Genetic Algorithms
- **NEAT (NeuroEvolution of Augmenting Topologies)** – Evolve network structure (nodes/layers) alongside weights.
- **LSTM & Transformer Network Support** – Recurrent topologies for time‑series memory, loadable as plugins.
- **SHAP Value Export** – Post‑optimisation analysis showing which genes/parameters impacted fitness most.
- **Island Model GA** – Multiple isolated populations run in parallel, exchanging “immigrant” chromosomes to maintain diversity.
- **Adaptive Mutation Rates (1/5th Rule)** – Dynamically scale the mutation rate based on recent success rate.
- **Out‑of‑Sample Walk‑Forward API** – Automatically transition into out‑of‑sample testing after an anchored GA completes.
- **Gene Constraint Dependencies** – Allow one gene’s `Max` to depend dynamically on another’s current value (e.g. `FastPeriod < SlowPeriod`).

### 6. Infrastructure, Operations & Observability
- **Multi‑Engine Support** – Run multiple isolated trading engines in the same process with separate configs and telemetry.
- **Production Host Application** – Robust, configurable host (console / Windows Service / Docker) with DI, logging, lifecycle management.
- **Docker Swarm / K8s Orchestration** – Helm charts to spin up 50+ headless engines for grid optimisation.
- **Web Dashboard & UI** – Web‑based monitoring of live trading, backtest reports, and optimisation control.
- **Alerting & Notifications** – Generic integration with email, Telegram, Slack for critical events (margin, connection loss, stop‑out).
- **Real‑Time Risk Aggregation** – Aggregate risk across multiple live engines in a central dashboard.
- **Prometheus / Grafana Exporter** – Expose `ChronosMetrics` via a dedicated HTTP endpoint on the engine.
- **Redis Message Bus Adapter** – Push events to Redis for cross‑server engine coordination.
- **gRPC Control API** – Multiplexed gRPC for engine‑cloud communication (alternative to WebSockets).
- **REST API** – Expose backtesting, optimisation, and live control via HTTP endpoints.
- **Hardware Intrusion Detection** – Detect memory‑scanning tools (e.g. CheatEngine) to protect deployed proprietary strategies.

### 7. Developer Experience & SDK
- **Jupyter Notebook Integration** – Python bridge to query `Chronos.Core.Kernel` backtests directly from Pandas.
- **F# / Python Bindings** – Enable strategy development in other languages via interop or embedded scripting.
- **Mock Exchange Adapter** – Highly realistic local matching engine that simulates network latency and order book queues.
- **Indicator Composition DSL** – Write `Indicators.Get("RSI(SMA(14),14)")` using a string‑based domain language.
- **Replay Mode GUI** – Lightweight Avalonia/WPF tool to step through completed backtests bar‑by‑bar visually.
- **State Serialization (Live)** – Dump `LiveBroker` state to disk on shutdown, hydrate on startup for crash recovery without re‑fetching from exchange.

---

All 40 original items and the supplementary features are now integrated under a coherent set of categories, with no duplication. This can serve as the long‑term product roadmap.
