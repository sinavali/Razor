# Razor Future Features

**Version:** 1.0.0 LTS (current)  
**Audience:** Internal & partner teams  
**Status:** Forward‑looking roadmap – no commitments implied  
**Last Updated:** 2026-07-09  

---

This document collects all planned features for Razor beyond the current v1.0.0 LTS.  
It is organised by category to guide long‑term development. References to obsolete plugin types (e.g., `IExecutionAlgorithm`, `IRiskManager`, `IFitnessModel`, `ISimulationFriction`) have been updated to reflect the hooks‑and‑slots architecture. Features are listed for consideration; none constitute a commitment for any specific release.

---

## Trading & Execution

- **Smart Order Routing (SOR)** – Meta‑adapter that routes orders across multiple brokers based on best bid/ask. Implemented as an `IAdapterCapability` that aggregates child adapters.
- **Iceberg / Hidden Orders** – Randomise large orders into smaller market slices. Exposed as hook plugins that register on `backtest.order.before_execute` and `live.order.before_send`, splitting volume and scheduling slices.
- **TWAP / VWAP Execution Algorithms** – Built‑in execution strategies triggered by strategy order parameters. Hook plugins can register on the execution hooks to implement custom slicing logic.
- **Post‑Only Limit Orders** – Flag orders to guarantee maker rebates, cancelling/re‑pricing if they would cross the spread. Implemented as an order parameter respected by the adapter.
- **Local Order Book SL/TP** – Maintain stop‑loss/take‑profit levels engine‑side and fire market orders when triggered. Hook plugins on `backtest.position.sl_tp_check` (future hook point) can implement trailing stops.
- **Cross‑Margin Portfolio Sizing** – Size positions based on total portfolio risk rather than isolated symbol equity. Hook plugins on `backtest.order.validation` or `live.order.validation` can enforce portfolio‑level risk limits.
- **Kill‑Switch Triggers** – Global equity drop thresholds that instantly liquidate and disconnect adapters. Implemented as a hook plugin with action hooks on `live.equity.changed`.
- **OCO (One‑Cancels‑Other) Orders** – Link a stop‑loss and limit order so that filling one cancels the other. Implemented by the broker or via adapter logic.
- **Trailing Stops** – Dynamic stop‑loss that moves with the price. Hook plugin on `backtest.tick.after` modifies SL on open positions.
- **Bracket Orders** – Entry, stop‑loss, and take‑profit combined into an atomic group.
- **Multi‑Leg Orders** – Support spread trading, pairs trading, and other multi‑instrument orders.
- **Partial Fills Simulation** – Realistic fill simulation that takes order‑book depth into account.
- **Market Impact Models** – Slippage models that adjust for order size relative to available liquidity. Implemented within the adapter's `IMarketCalculator`.

---

## Risk & Compliance

- **Value at Risk (VaR) Modelling** – Historical and parametric VaR calculated in real time during live trading. Hook plugin on `live.equity.changed` and `live.tick.processed`.
- **Audit Trail Logging** – Immutable, write‑only SQLite logs of every broker API request for regulatory compliance. Hook plugin on `live.order.executed` and `live.order.rejected`.
- **Fat‑Finger Limits** – Hard‑coded maximum order sizes that override any strategy request. Hook plugin on `backtest.order.validation` / `live.order.validation`.
- **Margin Call Predictive Alerts** – Notify via Telegram/Email if the current drawdown trajectory will hit stop‑out within one hour. Hook plugin on `live.equity.changed`.
- **Wash Trading Prevention** – Reject orders that would execute against the strategy's own resting limit orders. Hook plugin on `live.order.validation`.

---

## Data, Analytics & Research

- **L2/L3 Order Book Replay** – Support `DepthTick` structs for DOM‑based backtesting. Adapters with depth support register additional tick types.
- **Footprint / Volume Profile Generation** – Synthesise volume profiles locally from raw ticks. Implemented as indicators.
- **Synthetic Spread Symbols** – Define virtual symbols (e.g. `EURUSD‑GBPUSD`) that generate synthetic ticks. Implemented as a specialised indicator.
- **Walk‑Forward 3D Surface Plots** – Export JSON for Plotly.js to render 3D optimisation landscapes. Hook plugin on `optimization.generation.completed`.
- **Trade Correlation Matrix** – Show overlapping exposure times between strategies/symbols after a backtest. Hook plugin on `backtest.completed`.
- **Slippage Heatmaps** – Track expected slippage by time‑of‑day and volume. Hook plugin on `backtest.order.after_execute`.
- **Custom Tick Synthesizer Profiles** – User‑defined tick generation behaviour (random walk vs. historical spread) for legacy bar conversion.
- **Cloud‑Hosted Tick Datalake** – Central repository streaming historical MMF chunks to engines via gRPC.
- **Cloud Storage Adapters** – Read historical data directly from AWS S3, Azure Blob, etc.
- **Multi‑Exchange Data Aggregation** – Composite adapter that merges liquidity from multiple exchanges for a single symbol.
- **Data Quality Checks** – Automatic validation for gaps, spikes, and staleness before backtesting or live use.
- **Tick Data Compression & Streaming** – Compressed binary format; stream from cloud storage without full download.
- **Monte Carlo Simulation** – Randomised backtest variations to assess strategy robustness. Implemented as Cloud‑orchestrated logic using repeated backtest calls.
- **Sensitivity Analysis** – Vary individual parameters and measure performance impact. Cloud‑orchestrated.
- **Custom Report Templates** – User‑definable report layouts (HTML, PDF) with charts and statistics. Hook plugins on `report.before_generate` and `report.after_generate`.

---

## Machine Learning & Genetic Algorithms

- **NEAT (NeuroEvolution of Augmenting Topologies)** – Evolve network structure (nodes/layers) alongside weights. Implemented as an `INeuralNetworkModel` slot capability. The model's `ParameterCount` dynamically varies per chromosome.
- **LSTM & Transformer Network Support** – Recurrent topologies for time‑series memory, loadable as `INeuralNetworkModel` implementations (likely via ONNX).
- **SHAP Value Export** – Post‑optimisation analysis showing which genes/parameters impacted fitness most. Hook plugin on `optimization.completed`.
- **Island Model GA** – Multiple isolated populations run in parallel, exchanging "immigrant" chromosomes to maintain diversity. Internal GA enhancement.
- **Adaptive Mutation Rates (1/5th Rule)** – Dynamically scale the mutation rate based on recent success rate. Internal GA enhancement; hook plugins can observe via `optimization.generation.completed`.
- **Out‑of‑Sample Walk‑Forward API** – Automatically transition into out‑of‑sample testing after an anchored GA completes. Cloud‑orchestrated; the engine's GA and backtest primitives are called in sequence.
- **Gene Constraint Dependencies** – Allow one gene's `Max` to depend dynamically on another's current value (e.g. `FastPeriod < SlowPeriod`). Extension of the `[Gene]` attribute.

---

## Infrastructure, Operations & Observability

- **Multi‑Engine Support** – Run multiple isolated trading engines in the same process with separate configs and telemetry.
- **Production Host Application** – Robust, configurable host (console / Windows Service / Docker) with DI, logging, lifecycle management.
- **Docker Swarm / K8s Orchestration** – Helm charts to spin up 50+ headless engines for grid optimisation.
- **Web Dashboard & UI** – Web‑based monitoring of live trading, backtest reports, and optimisation control (part of Razor Cloud).
- **Alerting & Notifications** – Generic integration with email, Telegram, Slack for critical events (margin, connection loss, stop‑out). Hook plugins on `live.*` and `backtest.completed` action hooks.
- **Real‑Time Risk Aggregation** – Aggregate risk across multiple live engines in a central dashboard.
- **Prometheus / Grafana Exporter** – Expose `CoreMetrics` via a dedicated HTTP endpoint on the engine. Could be a hook plugin on `backtest.tick.completed` and `live.equity.changed`.
- **Redis Message Bus Adapter** – Push events to Redis for cross‑server engine coordination. Hook plugin on all major hooks.
- **gRPC Control API** – Multiplexed gRPC for engine‑cloud communication (alternative to WebSockets).
- **REST API** – Expose backtesting, optimisation, and live control via HTTP endpoints.
- **Hardware Intrusion Detection** – Detect memory‑scanning tools (e.g. CheatEngine) to protect deployed proprietary strategies.

---

## Developer Experience & SDK

- **Jupyter Notebook Integration** – Python bridge to query `Razor.Kernel` backtests directly from Pandas.
- **F# / Python Bindings** – Enable strategy development in other languages via interop or embedded scripting.
- **Mock Exchange Adapter** – Highly realistic local matching engine that simulates network latency and order book queues. Built as a sample `IAdapterCapability` implementation.
- **Indicator Composition DSL** – Write `Indicators.Get("RSI(SMA(14),14)")` using a string‑based domain language.
- **Replay Mode GUI** – Lightweight Avalonia/WPF tool to step through completed backtests bar‑by‑bar visually.
- **State Serialization (Live)** – Dump `LiveBroker` state to disk on shutdown, hydrate on startup for crash recovery without re‑fetching from exchange.
- **Hook Debugging Tools** – Log hook execution chains, measure hook callback duration, detect hook conflicts at the same priority level.
- **Extension Validation Tool** – CLI tool that validates an extension DLL before upload: SDK version, interface implementation completeness, hook registration validity.

---

## Future Hook Points

The following hook points are candidates for addition in future minor releases:

| Proposed Hook | Pipeline | Type | Description |
|---------------|----------|------|-------------|
| `backtest.position.sl_tp_check` | Backtest | Action | Called when a tick triggers a SL/TP check before execution. |
| `live.position.sl_tp_check` | Live | Action | Called when a tick triggers a SL/TP check before sending to exchange. |
| `backtest.warmup_completed` | Backtest | Action | Called when warm‑up period ends. |
| `live.connection_lost` | Live | Action | Called when the adapter disconnects. |
| `live.connection_restored` | Live | Action | Called after successful reconnection. |
| `optimization.best_improved` | Optimisation | Action | Called when a new best fitness is found. |

---

*This document is a living wish‑list. Features are added or removed as business priorities evolve. None of the items above constitute a commitment for any specific release.*