# README.md — Razor.Core.Sdk

**Location:** `core/src/Razor.Core.Sdk/README.md`  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

# Razor.Core.Sdk

**Public SDK for Razor extension development**  
Version: 1.0.0 LTS

This package contains only contracts – interfaces, records, enums, and utilities – with **no runtime logic**. It is the only dependency you need to build extensions for Razor (adapters, strategies, indicators, hook plugins, and neural network models).

---

## Architecture

Razor extensions are organized into three concepts:

| Concept | What It Does | Directory |
|---------|-------------|-----------|
| **Slots** | Required capabilities (Adapter, Strategy, NN Model) | `Adapters/`, `Strategies/`, `NeuralNetworks/` |
| **Indicators** | Technical analysis computations | `Indicators/` |
| **Hooks** | Intercept and observe engine events | `Plugins/` (any scanned directory) |

A single DLL can combine any of these – for example, a strategy that also registers hooks.

---

## Namespaces

| Namespace | Description |
|-----------|-------------|
| `Razor.Core.Sdk.Hooks` | Hook registration interfaces (`IHookManifest`, `IHookRegistry`, `IFilterRegistration<T>`, `IActionRegistration<T>`), filter result types, and hook context interfaces for backtest, live, optimisation, and report pipelines. |
| `Razor.Core.Sdk.Slots` | Capability interfaces for the three slot types: `IAdapterCapability`, `IStrategyCapability`, and `INeuralNetworkModel`. |
| `Razor.Core.Sdk.Shared` | Domain types (`Tick`, `Position`, `Order`, `TimeFrame`, `SymbolProperties`, etc.), enums, exceptions, helpers, and base classes (`Indicator`, `StrategyBase`, `CustomizedRandom`). |

---

## Hook System

Hooks are the primary extensibility mechanism. A hook plugin implements `IHookManifest` and registers callbacks on named hook points with priorities:

- **Filter hooks** – transform or reject data flowing through the pipeline (e.g., order validation).
- **Action hooks** – observe events without modifying data (e.g., send a notification).

Hook callbacks are strongly typed. Action hook callbacks **must be synchronous** – do not use `async void`; exceptions inside async void crash the process. See the Extension Developer Guide for safe async patterns.

### Hook Pipelines

| Pipeline | Sub‑Registry | Description |
|----------|--------------|-------------|
| Backtesting | `IBacktestHooks` | Tick filtering, order validation, position events, equity updates, completion |
| Live Trading | `ILiveHooks` | Tick processing, order validation, execution reports, position events, sync, reconnection |
| Optimisation | `IOptimizationHooks` | Generation lifecycle, chromosome creation/evaluation, selection, crossover, mutation, stagnation |
| Reports | `IReportHooks` | Pre‑generation filtering, post‑generation actions |

---

## Slot Capabilities

| Slot | Interface | Description |
|------|-----------|-------------|
| Adapter | `IAdapterCapability` | Connects to a broker/exchange. Provides historical data, live streaming, and execution. |
| Strategy | `IStrategyCapability` | Trading logic – `OnTick(string symbol, Tick tick)`. Supports gene‑based optimisation. |
| Neural Network | `INeuralNetworkModel` | Feed‑forward, ONNX, LSTM, RL models. Unified parameter‑vector interface compatible with GA. |

---

## Key Types

### Domain Types

| Type | Description |
|------|-------------|
| `Tick` | A single price update: timestamp, bid, ask, volume. Blittable for memory‑mapped I/O. |
| `Position` | Immutable record of an open or completed trade. |
| `Order` | A pending (untriggered) order. |
| `TimeFrame` | Enum representing aggregation periods: `Tick`, `M1`, `M5`, `H1`, `D1`, etc. |
| `SymbolProperties` | Exchange‑specific metadata: tick size, contract size, margin rates, swap rates, etc. |

### Helpers

| Type | Description |
|------|-------------|
| `StrategyBase` | Convenience base class for strategies. Provides broker, tick window, indicators, and helper methods. |
| `Indicator` | Base class for technical indicators. Manages a circular buffer with pooled arrays. |
| `GeneInjector` | Static helper for gene extraction, injection, and schema building. |
| `CustomizedRandom` | Portable deterministic PRNG (xorshift128+). Guarantees identical sequences across .NET versions. |
| `TickWindow` | Sliding ring buffer of recent ticks per symbol. Provides on‑demand OHLC aggregation. |
| `TickSynthesizer` | Converts bar data into synthetic tick arrays. |

### Exceptions

| Type | Description |
|------|-------------|
| `ConfigurationException` | Thrown when a specification contains invalid values. |
| `AdapterException` | Thrown when an adapter operation fails. |
| `StrategyException` | Thrown when a strategy encounters an error. |
| `OptimizationException` | Thrown during GA optimisation failures. |

---

## Documentation

- [Razor Principles](../../docs/Razor%20Principles.md)
- [Configuration Reference](../../docs/Razor%20Configuration%20Reference.md)
- [Extension Developer Guide](../../docs/Razor%20Extension%20Developer%20Guide.md)

---

## Quick Start

1. Create a .NET class library targeting `net10.0`.
2. Add this package:
   ```xml
   <PackageReference Include="Razor.Core.Sdk" Version="1.0.0" />
   ```
3. Add the SDK version attribute:
   ```csharp
   [assembly: SdkVersion("1.0.0")]
   ```
4. Implement one or more contracts (`IHookManifest`, `IStrategyCapability`, etc.).
5. Build and place the DLL in the appropriate engine directory.

---

## Versioning

This package follows [Semantic Versioning](https://semver.org). Breaking changes only occur in major releases.

**Current:** v1.0.0 LTS

---

## License

Proprietary. Redistribution allowed for extension development purposes only.