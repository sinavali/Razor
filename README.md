# Chronos Core

**Institutional‑grade algorithmic trading engine – v1.0.0 LTS**

`chronos-core` is the heart of the Chronos ecosystem. It contains the public SDK for plugin developers and the closed‑source core engine that executes backtests, optimisations, and live trading. This repository is the single source of truth for all Chronos trading logic.

---

## Architecture

```
┌──────────────────────────────────────────────┐
│                  Chronos Cloud               │  (SaaS – management & monitoring)
└──────────────────┬───────────────────────────┘
                   │ encrypted WebSocket
┌──────────────────▼───────────────────────────┐
│              Chronos Engine                   │  (headless binary – future)
└──────────────────┬───────────────────────────┘
                   │
┌──────────────────▼───────────────────────────┐
│          Chronos.Core.Kernel                  │  (closed‑source core)
│  backtesting • optimisation • live trading    │
│  genetic algorithm • telemetry • plugins      │
└──────────────────┬───────────────────────────┘
                   │ references
┌──────────────────▼───────────────────────────┐
│       Chronos.Core.Abstractions               │  (public NuGet SDK)
│  interfaces • records • enums • utilities     │
└──────────────────────────────────────────────┘
```

- **`Chronos.Core.Abstractions`** – The only dependency for plugin developers. Contains zero runtime logic.
- **`Chronos.Core.Kernel`** – Closed‑source implementation of all trading and optimisation algorithms.
- **`Chronos.Engine`** *(future)* – The production executable that hosts the Kernel and communicates with Chronos Cloud.

---

## Supported Plugin Types (v1.0.0 LTS)

Developers can create and distribute the following plugin types:

| Plugin Type | Interface | Discovery Attribute |
|-------------|-----------|---------------------|
| Adapter | `IAdapter` | `[AdapterName]` |
| Strategy | `IStrategy` / `StrategyBase` | `[StrategyName]` |
| Indicator | `Indicator` | *(via registry)* |
| Risk Manager | `IRiskManager` | `[RiskManagerName]` |
| Position Sizer | `IPositionSizer` | `[PositionSizerName]` |
| Market Regime Detector | `IMarketRegimeDetector` | `[MarketRegimeDetectorName]` |
| Execution Algorithm | `IExecutionAlgorithm` | `[ExecutionAlgoName]` |
| Fitness Model | `IFitnessModel` | `[FitnessModelName]` |
| Simulation Friction | `ISimulationFriction` | *(via spec)* |
| Notification Channel | `INotificationChannel` | `[NotificationChannelName]` |
| Metrics Provider | `IMetricsProvider` | `[MetricsProviderName]` |
| Report Generator | `IReportGenerator` | `[ReportGeneratorName]` |
| Market Data Provider | `IMarketDataProvider` | `[MarketDataProviderName]` |

All plugins share a common versioning and isolation model via `AssemblyLoadContext`.

---

## Repository Structure

```
chronos-core/
├── src/
│   ├── Chronos.Core.Abstractions/     ← Public SDK (NuGet package)
│   └── Chronos.Core.Kernel/           ← Closed‑source engine
├── tests/
│   ├── Chronos.Core.Abstractions.UnitTests/
│   ├── Chronos.Core.Abstractions.IntegrationTests/
│   ├── Chronos.Core.Kernel.UnitTests/          (future)
│   ├── Chronos.Core.Kernel.IntegrationTests/   (future)
│   └── Chronos.Determinism.Tests/              (future – golden determinism gate)
├── docs/                              ← Repository‑level documentation
├── Directory.Build.props
├── global.json
├── nuget.config
├── VERSION.txt
└── README.md
```

---

## Documentation

- **[Chronos Principles](docs/Chronos%20Principles.md)** – Immutable architectural rules for all Chronos projects.
- **[Product Model](docs/Chronos%20Product%20Model.md)** – Business and product definition.
- **[Glossary](docs/Chronos%20Glossary.md)** – All domain terms defined.
- **[Configuration Reference](docs/Chronos%20Configuration%20Reference.md)** – Every configuration object, field, and validation rule.
- **[Plugin Developer Guide](docs/Chronos%20Plugin%20Developer%20Guide.md)** – How to build adapters, strategies, indicators, and all other plugin types.
- **[Installation & Deployment Guide](docs/Chronos%20Installation%20%26%20Deployment%20Guide.md)** – How to install the Chronos Engine on Windows/Linux.
- **[Internal Architecture](docs/Chronos%20Internal%20Technical%20Architecture%20Document.md)** – Closed‑source engine internals (for core developers only).

---

## Quick Start – Plugin Developers

1. Install the `Chronos.Core.Abstractions` NuGet package (version `1.0.0`).
2. Follow the [Plugin Developer Guide](docs/Chronos%20Plugin%20Developer%20Guide%20(v1.0.0%20LTS).md).
3. Test your plugin locally by placing the DLL in the engine’s `plugins/` folder.
4. Upload finished plugins to Chronos Cloud for distribution.

---

## Quick Start – Core Developers (Internal)

### Prerequisites

- .NET 10 SDK (`10.0.300` or later, see `global.json`)
- A local Gitea instance (or access to the Chronos package feed)

### Build

```bash
git clone http://localhost:300/Chronos/chronos-core.git
cd chronos-core
dotnet restore
dotnet build --configuration Release
```

### Test

```bash
dotnet test --configuration Release --verbosity normal
```

The determinism golden test suite (`tests/Chronos.Determinism.Tests`) runs separately and is a mandatory CI gate.

### Pack (local development)

```bash
# Build and pack Abstractions
dotnet pack src/Chronos.Core.Abstractions/Chronos.Core.Abstractions.csproj --configuration Release --output ./nupkgs

# Build and pack Kernel (use local Abstractions reference)
dotnet pack src/Chronos.Core.Kernel/Chronos.Core.Kernel.csproj --configuration Release -p:UseLocalAbstractions=true --output ./nupkgs
```

For publishing to a NuGet feed, set `UseLocalAbstractions=false` so the Kernel package declares a proper dependency on the Abstractions package.

---

## Long‑Term Support (LTS)

Version `1.0.0` is an **LTS release**. It will receive critical bug fixes and security patches until the next LTS major version is released. Breaking changes are reserved for major version boundaries (`2.0.0`, `3.0.0`, etc.).

- **Major (X.0.0):** May break public APIs and alter backtest determinism.
- **Minor (X.Y.0):** Adds features without breaking changes or determinism.
- **Patch (X.Y.Z):** Critical fixes only.

See [Chronos Principles – Principle 17](docs/Chronos%20Principles.md#17-versioning--longterm-support) for the full versioning policy.

---

## Licensing

- **`Chronos.Core.Abstractions`** – Proprietary, freely redistributable. May be open‑sourced in the future.
- **`Chronos.Core.Kernel`** – Closed‑source, all rights reserved. Distributed only as part of the Chronos Engine binary.

---

## Community & Support

- **Plugin developers:** Use the [Plugin Developer Guide](docs/Chronos%20Plugin%20Developer%20Guide%20(v1.0.0%20LTS).md) and the `Chronos.Samples` repository.
- **Engine users:** All support is handled through Chronos Cloud.
- **Core contributors:** See the internal architecture document and contact the Chronos architecture board for design decisions.
