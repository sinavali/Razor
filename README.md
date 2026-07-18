# Solution README.md — Razor.Core

**Location:** `README.md` (Root of the Razor.Core solution)  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

# Razor.Core

**Institutional‑grade algorithmic trading engine – v1.0.0 LTS**

Welcome to the Razor.Core solution. This repository contains the heart of the Razor ecosystem: the public SDK for extension developers, the shared utilities for high‑performance I/O, the closed‑source engine that executes backtests, optimisations, and live trading, and the headless engine executable that connects to Razor Cloud.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Razor Cloud                            │
│                      (SaaS – management & monitoring)              │
└──────────────────────────────┬──────────────────────────────────────┘
                                │ encrypted WebSocket
┌──────────────────────────────▼──────────────────────────────────────┐
│                          Razor Engine                            │
│                   (headless executable – closed source)             │
└──────────────────────────────┬──────────────────────────────────────┘
                                │
┌──────────────────────────────▼──────────────────────────────────────┐
│                        Razor.Core.Kernel                         │
│                     (closed‑source core library)                    │
│  backtesting • optimisation • live trading • genetic algorithm      │
│  brokers • hooks • telemetry • message bus                         │
└──────────────────────────────┬──────────────────────────────────────┘
                                │
┌──────────────────────────────▼──────────────────────────────────────┐
│                        Razor.Core.Shared                         │
│                   (shared utilities – closed source)                │
│       memory‑mapped tick lists • binary file mapping               │
└──────────────────────────────┬──────────────────────────────────────┘
                                │ references
┌──────────────────────────────▼──────────────────────────────────────┐
│                         Razor.Core.Sdk                           │
│                      (public NuGet SDK – open contracts)            │
│             hooks • slots • domain types • utilities               │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Design Principles

| Principle | Description |
|-----------|-------------|
| **Determinism** | Given the same inputs, Razor produces bit‑identical outputs on every run. |
| **Tick‑Only Core** | All operations use raw ticks; OHLC aggregated on‑demand. |
| **Market Agnosticism** | Razor has zero knowledge of any specific market type; all exchange logic resides in adapters. |
| **Live‑Backtest Parity** | Simulated and live brokers use the same `IMarketCalculator` for identical behaviour. |
| **Cloud‑First** | The Engine is a thin client; all configuration, scheduling, and reporting reside in the Cloud. |
| **Infinite Resiliency** | If Cloud connectivity is lost, the Engine retries forever and never exits. |
| **Extension Isolation** | Extensions are loaded in isolated `AssemblyLoadContext`s; hot‑reloadable. |

---

## Projects

| Project | Description | Visibility |
|---------|-------------|------------|
| `Razor.Core.Sdk` | Public SDK for building extensions (adapters, strategies, indicators, hook plugins, NN models). Contains only contracts – no runtime logic. | NuGet package (proprietary, freely redistributable) |
| `Razor.Core.Shared` | Shared utilities for high‑performance I/O: memory‑mapped tick files (`MemoryMappedTickList`), binary file mapping (`BinaryDataMapper`), and borrowed data management (`BorrowedTickData`). | Private (closed‑source) |
| `Razor.Core.Kernel` | Core engine implementing all trading logic: backtesting, optimisation, live trading, brokers (simulated and live), genetic algorithm, hooks, telemetry, and message bus. | Private (closed‑source) |
| `Razor.Core.Engine` | Headless executable that hosts the Kernel, manages extensions, and communicates with Razor Cloud via encrypted WebSocket. Includes CLI, service support, self‑update, and command dispatch (60+ commands). | Private (closed‑source) |

## Quick Start – Extension Developers

```
Razor.Core.Sdk
       ↑
Razor.Core.Shared
       ↑
Razor.Core.Kernel
       ↑
Razor.Core.Engine
```

Extensions (adapters, strategies, indicators, hook plugins, NN models) reference **only** `Razor.Core.Sdk`.

---

## Quick Start

### For Extension Developers

1. Install the `Razor.Core.Sdk` NuGet package in your .NET class library targeting `net10.0`.
2. Add the SDK version attribute:
   ```csharp
   [assembly: SdkVersionAttribute("1.0.0")]
   ```
3. Implement one or more contracts:
   - `IAdapterCapability` – for broker connectivity
   - `IStrategyCapability` – for trading logic
   - `INeuralNetworkModel` – for neural network models
   - `Indicator` – for technical indicators
   - `IHookManifest` – for hook plugins
4. Build your DLL and place it in the appropriate engine directory.
5. Manage activation via Razor Cloud.

For detailed guidance, see the [Extension Developer Guide](docs/Razor%20Extension%20Developer%20Guide.md).

### For Core Developers

#### Prerequisites
- .NET 10 SDK (`10.0.300` or later, see `global.json`)

### Build
```bash
dotnet restore
dotnet build --configuration Release
```

### Test
```bash
dotnet test --configuration Release
```

#### Run the Engine

The commands below assume you are already in the repository root (see the [Build](#build) step).

```bash
cd src/Razor.Core.Engine
dotnet run -- --auth=username,password,apikey
```

Or run interactively (prompts for credentials):

```bash
dotnet run
```

#### Run as a Service

**Windows:**
```bash
sc create RazorEngine binPath = "C:\Path\Razor.Core.Engine.exe --service --auth=user,pass,key" start=auto
```

**Linux (systemd):**
```ini
[Service]
ExecStart=/opt/Razor/Razor.Core.Engine --service --auth=user,pass,key
WorkingDirectory=/opt/Razor
Restart=on-failure
```

---

## Repository Structure

```
.
├── src/
│   ├── Razor.Core.Sdk/          ← Public contracts (NuGet package)
│   │   ├── Hooks/                 ← Hook registration interfaces and contexts
│   │   ├── Shared/                ← Domain types, enums, exceptions, helpers
│   │   └── Slots/                 ← Capability interfaces (Adapter, Strategy, NN)
│   ├── Razor.Core.Shared/       ← Shared utilities (memory‑mapped I/O)
│   ├── Razor.Core.Kernel/       ← Core engine implementation
│   │   ├── Backtesting/           ← Backtest runner, input, result
│   │   ├── Brokers/               ← SimulatedBroker, LiveBroker
│   │   ├── Clock/                 ← TickClock, SystemClock
│   │   ├── Configuration/         ← ExecutionSpec, OptimizationSpec, LiveSpec
│   │   ├── Events/                ← Domain events (BacktestCompleted, etc.)
│   │   ├── Hooks/                 ← Hook registry, invoker, contexts
│   │   ├── Indicators/            ← Indicator registry
│   │   ├── Messaging/             ← Message bus
│   │   ├── Metrics/               ← FitnessCalculator, MetricsCalculator
│   │   ├── NeuralNetworks/        ← FeedForwardNetwork (built‑in)
│   │   ├── Optimization/          ← GeneticOptimizer, Chromosome, Runner
│   │   ├── Reporting/             ← ReportGenerator (stub; Cloud renders reports)
│   │   └── Telemetry/             ← CoreMetrics (OpenTelemetry)
│   └── Razor.Core.Engine/       ← Headless executable
│       ├── Communication/         ← CloudConnector, BinaryTransferManager
│       ├── Core/                  ← Security, State, Credentials, Logging
│       ├── Extensions/            ← ExtensionManager, PluginLoadContext
│       ├── Kernel/                ← KernelService (facade)
│       ├── Management/            ← CommandDispatcher, TaskManager, CronJobManager
│       ├── Services/              ← BehaviorRecorder, SelfUpdateManager
│       └── Program.cs             ← Entry point
├── tests/
│   ├── Razor.Core.Sdk.UnitTests/       ← 200+ unit tests
│   └── Razor.Core.Sdk.IntegrationTests/ ← Integration tests
├── docs/                           ← Core documentation
│   ├── Razor Principles.md
│   ├── Razor Configuration Reference.md
│   ├── Razor Extension Developer Guide.md
│   ├── Razor Installation & Deployment Guide.md
│   ├── Razor Engine – Finalised Technical Blueprint.md
│   └── Razor Internal Technical Architecture Document.md
├── Razor.Core.sln               ← Solution file
├── Directory.Build.props          ← Common build properties
├── global.json                    ← SDK version
├── nuget.config                   ← Package sources
├── .editorconfig                  ← Code style rules
└── .gitignore                     ← Git ignore rules
```

---

## Documentation

| Document | Audience | Description |
|----------|----------|-------------|
| [Razor Principles](docs/Razor%20Principles.md) | All teams | Immutable architectural rules governing every Razor project. |
| [Configuration Reference](docs/Razor%20Configuration%20Reference.md) | Extension developers & power users | Complete catalog of configuration objects, enums, and validation rules. |
| [Extension Developer Guide](docs/Razor%20Extension%20Developer%20Guide.md) | Extension developers | Comprehensive guide for building adapters, strategies, indicators, hook plugins, and NN models. |
| [Installation & Deployment Guide](docs/Razor%20Installation%20%26%20Deployment%20Guide.md) | End‑users & IT staff | Step‑by‑step installation, configuration, and troubleshooting. |
| [Engine Technical Blueprint](docs/Razor%20Engine%20–%20Finalised%20Technical%20Blueprint.md) | Core developers | Complete engine specification: CLI, communication protocol, commands, security. |
| [Internal Architecture Document](docs/Razor%20Internal%20Technical%20Architecture%20Document.md) | Core developers | Data flow, broker architecture, hook system, GA engine, threading, telemetry. |
| [Product Model](docs/Razor%20Product%20Model.md) | All teams | Product overview, components, licensing, and workflows. |
| [Glossary](docs/Razor%20Glossary.md) | All users | Definitions of all domain‑specific terms. |
| [Future Features](docs/Razor%20Future%20Features.md) | Internal & partners | Long‑term roadmap of planned features. |
| [Project Overview](docs/Razor%20Proposal.md) | External | High‑level introduction to Razor. |

---

## Key Features

| Feature | Description |
|---------|-------------|
| **Tick‑Only Core** | All operations use raw ticks; OHLC aggregated on‑demand. |
| **Deterministic** | Bit‑identical outputs across runs; golden tests enforce determinism. |
| **Live‑Backtest Parity** | Simulated and live brokers use the same `IMarketCalculator`. |
| **Genetic Algorithm** | Population‑based optimisation with elitism, tournament selection, crossover, mutation, and hyper‑mutation. |
| **Neural Network Support** | Unified `INeuralNetworkModel` interface; built‑in feed‑forward network. |
| **Hook System** | Priority‑based extensibility with filter and action hooks across backtest, live, optimisation, and report pipelines (50+ hook points). |
| **Extension Isolation** | Loaded in isolated `AssemblyLoadContext`s; hot‑reloadable. |
| **Cloud‑Connected** | Persistent encrypted WebSocket; infinite retry; remote command execution. |
| **Self-Update** | Automatic binary updates via Cloud heartbeat. |
| **Behavior Logging** | Sparse behaviour recording for RL training with MessagePack + GZip compression. |
| **Performance** | Memory‑mapped files, pooled arrays, allocation‑free hot paths. |

---

## Versioning

| Version | Status | Description |
|---------|--------|-------------|
| **1.0.0 LTS** | Current | Stable release with hooks‑and‑slots extension system, GA optimisation, live trading, Cloud connectivity, and 60+ commands. |
| **1.5.0** | Planned | Enhanced reporting, performance improvements, additional hook points. |
| **2.0.0 LTS** | Planned | Multi‑operation engine, advanced marketplace, ONNX and RL model support. |

Razor follows [Semantic Versioning](https://semver.org). Each major version is an LTS release.

---

## Command ID Registry

All 60+ commands are fully implemented in `Razor.Core.Engine.Management.Commands.Handlers`.

| Category | ID Range | Description |
|----------|----------|-------------|
| System Management | 1000‑1099 | Auth, heartbeat, shutdown, restart |
| Live Trading | 1100‑1199 | StartLive, StopLive, InjectGenes, PauseLive, ResumeLive, GetLiveState, SyncLive |
| Backtesting | 1200‑1299 | RunBacktest, CancelBacktest, GetBacktestResult |
| Optimisation | 1300‑1399 | StartOptimization, CancelOptimization, PauseOptimization, ResumeOptimization, GetOptimizationState, GetOptimizationResult |
| Extensions | 1400‑1499 | ReloadExtensions, DeployExtension, RemoveExtension, ListExtensions, ActivateExtensions |
| Reports | 1500‑1599 | GenerateReport, GetReport |
| Logs & Telemetry | 1600‑1699 | GetLogs, DeleteLogsAll, DeleteLogsExpired, SetLogLevel, GetMetrics, ExportMetrics |
| Schedules & Cron | 1700‑1799 | SetCronJob, DeleteCronJob, ListCronJobs, SetSchedule, DeleteSchedule, ListSchedules |
| Admin & Broadcast | 1900‑1999 | BroadcastMessage, SetAdminConfig, GetEngineCapabilities, GetEngineVersion |
| Kill & Emergency | 2000‑2099 | KillSwitch, EmergencyStop |
| Behaviour Logging | 2100‑2199 | EnableBehaviorLogging, DisableBehaviorLogging, GetBehaviorLogs, DeleteBehaviorLogs |

---

## Licensing

| Project | License |
|---------|---------|
| `Razor.Core.Sdk` | Proprietary, freely redistributable for extension development. |
| `Razor.Core.Shared` | Closed‑source, all rights reserved. |
| `Razor.Core.Kernel` | Closed‑source, all rights reserved. |
| `Razor.Core.Engine` | Closed‑source, distributed as part of the Razor Engine binary. |

---

## Contributing

This repository is closed‑source. Contribution is restricted to Razor core team members.

For extension development, please refer to the [Extension Developer Guide](docs/Razor%20Extension%20Developer%20Guide.md).

---

## Support

- **Documentation:** See the `docs/` directory.
- **Issues:** Contact Razor support through the Cloud dashboard.
- **Community:** Visit the Razor developer forum (coming soon).

---

*This README is the authoritative entry point for the Razor.Core solution. All code, documentation, and design decisions must align with the [Razor Principles](docs/Razor%20Principles.md).*