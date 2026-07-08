# Solution README.md — Chronos.Core

**Location:** `core/README.md` (Root of the Chronos.Core solution)  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

# Chronos.Core

**Institutional‑grade algorithmic trading engine – v1.0.0 LTS**

Welcome to the Chronos.Core solution. This repository contains the heart of the Chronos ecosystem: the public SDK for extension developers, the shared utilities for high‑performance I/O, the closed‑source engine that executes backtests, optimisations, and live trading, and the headless engine executable that connects to Chronos Cloud.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Projects](#projects)
3. [Quick Start](#quick-start)
   - [For Extension Developers](#for-extension-developers)
   - [For Core Developers](#for-core-developers)
4. [Repository Structure](#repository-structure)
5. [Documentation](#documentation)
6. [Versioning](#versioning)
7. [Licensing](#licensing)
8. [Support](#support)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Chronos Cloud                            │
│                      (SaaS – management & monitoring)              │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ encrypted WebSocket
┌──────────────────────────────▼──────────────────────────────────────┐
│                          Chronos Engine                            │
│                   (headless executable – closed source)             │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────┐
│                        Chronos.Core.Kernel                         │
│                     (closed‑source core library)                    │
│  backtesting • optimisation • live trading • genetic algorithm      │
│  brokers • hooks • telemetry • message bus                         │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────┐
│                        Chronos.Core.Shared                         │
│                   (shared utilities – closed source)                │
│       memory‑mapped tick lists • binary file mapping               │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ references
┌──────────────────────────────▼──────────────────────────────────────┐
│                         Chronos.Core.Sdk                           │
│                      (public NuGet SDK – open contracts)            │
│             hooks • slots • domain types • utilities               │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Design Principles

| Principle | Description |
|-----------|-------------|
| **Determinism** | Given the same inputs, Chronos produces bit‑identical outputs on every run. |
| **Tick‑Only Core** | All operations use raw ticks; OHLC aggregated on‑demand. |
| **Market Agnosticism** | Chronos has zero knowledge of any specific market type; all exchange logic resides in adapters. |
| **Live‑Backtest Parity** | Simulated and live brokers use the same `IMarketCalculator` for identical behaviour. |
| **Cloud‑First** | The Engine is a thin client; all configuration, scheduling, and reporting reside in the Cloud. |
| **Infinite Resiliency** | If Cloud connectivity is lost, the Engine retries forever and never exits. |
| **Extension Isolation** | Extensions are loaded in isolated `AssemblyLoadContext`s; hot‑reloadable. |

---

## Projects

| Project | Description | Visibility |
|---------|-------------|------------|
| `Chronos.Core.Sdk` | Public SDK for building extensions (adapters, strategies, indicators, hook plugins, NN models). Contains only contracts – no runtime logic. | NuGet package (proprietary, freely redistributable) |
| `Chronos.Core.Shared` | Shared utilities for high‑performance I/O: memory‑mapped tick files (`MemoryMappedTickList`), binary file mapping (`BinaryDataMapper`), and borrowed data management (`BorrowedTickData`). | Private (closed‑source) |
| `Chronos.Core.Kernel` | Core engine implementing all trading logic: backtesting, optimisation, live trading, brokers (simulated and live), genetic algorithm, hooks, telemetry, and message bus. | Private (closed‑source) |
| `Chronos.Core.Engine` | Headless executable that hosts the Kernel, manages extensions, and communicates with Chronos Cloud via encrypted WebSocket. Includes CLI, service support, self‑update, and command dispatch (60+ commands). | Private (closed‑source) |

### Dependency Graph

```
Chronos.Core.Sdk
       ↑
Chronos.Core.Shared
       ↑
Chronos.Core.Kernel
       ↑
Chronos.Core.Engine
```

Extensions (adapters, strategies, indicators, hook plugins, NN models) reference **only** `Chronos.Core.Sdk`.

---

## Quick Start

### For Extension Developers

1. Install the `Chronos.Core.Sdk` NuGet package in your .NET class library targeting `net10.0`.
2. Add the SDK version attribute:
   ```csharp
   [assembly: SdkVersion("1.0.0")]
   ```
3. Implement one or more contracts:
   - `IAdapterCapability` – for broker connectivity
   - `IStrategyCapability` – for trading logic
   - `INeuralNetworkModel` – for neural network models
   - `Indicator` – for technical indicators
   - `IHookManifest` – for hook plugins
4. Build your DLL and place it in the appropriate engine directory.
5. Manage activation via Chronos Cloud.

For detailed guidance, see the [Extension Developer Guide](core/docs/Chronos%20Extension%20Developer%20Guide.md).

### For Core Developers

#### Prerequisites

- .NET 10 SDK (`10.0.300` or later, see `global.json`)
- Git

#### Clone and Build

```bash
git clone <repository-url>
cd core
dotnet restore
dotnet build --configuration Release
```

#### Run Tests

```bash
dotnet test --configuration Release
```

#### Run the Engine

```bash
cd src/Chronos.Core.Engine
dotnet run -- --auth=username,password,apikey
```

Or run interactively (prompts for credentials):

```bash
dotnet run
```

#### Run as a Service

**Windows:**
```bash
sc create ChronosEngine binPath = "C:\Path\Chronos.Core.Engine.exe --service --auth=user,pass,key" start=auto
```

**Linux (systemd):**
```ini
[Service]
ExecStart=/opt/chronos/Chronos.Core.Engine --service --auth=user,pass,key
WorkingDirectory=/opt/chronos
Restart=on-failure
```

---

## Repository Structure

```
core/
├── src/
│   ├── Chronos.Core.Sdk/          ← Public contracts (NuGet package)
│   │   ├── Hooks/                 ← Hook registration interfaces and contexts
│   │   ├── Shared/                ← Domain types, enums, exceptions, helpers
│   │   └── Slots/                 ← Capability interfaces (Adapter, Strategy, NN)
│   ├── Chronos.Core.Shared/       ← Shared utilities (memory‑mapped I/O)
│   ├── Chronos.Core.Kernel/       ← Core engine implementation
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
│   └── Chronos.Core.Engine/       ← Headless executable
│       ├── Communication/         ← CloudConnector, BinaryTransferManager
│       ├── Core/                  ← Security, State, Credentials, Logging
│       ├── Extensions/            ← ExtensionManager, PluginLoadContext
│       ├── Kernel/                ← KernelService (facade)
│       ├── Management/            ← CommandDispatcher, TaskManager, CronJobManager
│       ├── Services/              ← BehaviorRecorder, SelfUpdateManager
│       └── Program.cs             ← Entry point
├── tests/
│   ├── Chronos.Core.Sdk.UnitTests/       ← 200+ unit tests
│   └── Chronos.Core.Sdk.IntegrationTests/ ← Integration tests
├── docs/                           ← Core documentation
│   ├── Chronos Principles.md
│   ├── Chronos Configuration Reference.md
│   ├── Chronos Extension Developer Guide.md
│   ├── Chronos Installation & Deployment Guide.md
│   ├── Chronos Engine – Finalised Technical Blueprint.md
│   └── Chronos Internal Technical Architecture Document.md
├── Chronos.Core.sln               ← Solution file
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
| [Chronos Principles](core/docs/Chronos%20Principles.md) | All teams | Immutable architectural rules governing every Chronos project. |
| [Configuration Reference](core/docs/Chronos%20Configuration%20Reference.md) | Extension developers & power users | Complete catalog of configuration objects, enums, and validation rules. |
| [Extension Developer Guide](core/docs/Chronos%20Extension%20Developer%20Guide.md) | Extension developers | Comprehensive guide for building adapters, strategies, indicators, hook plugins, and NN models. |
| [Installation & Deployment Guide](core/docs/Chronos%20Installation%20%26%20Deployment%20Guide.md) | End‑users & IT staff | Step‑by‑step installation, configuration, and troubleshooting. |
| [Engine Technical Blueprint](core/docs/Chronos%20Engine%20–%20Finalised%20Technical%20Blueprint.md) | Core developers | Complete engine specification: CLI, communication protocol, commands, security. |
| [Internal Architecture Document](core/docs/Chronos%20Internal%20Technical%20Architecture%20Document.md) | Core developers | Data flow, broker architecture, hook system, GA engine, threading, telemetry. |
| [Product Model](../docs/Chronos%20Product%20Model.md) | All teams | Product overview, components, licensing, and workflows. |
| [Glossary](../docs/Chronos%20Glossary.md) | All users | Definitions of all domain‑specific terms. |
| [Future Features](../docs/Chronos%20Future%20Features.md) | Internal & partners | Long‑term roadmap of planned features. |
| [Project Overview](../docs/Chronos%20Proposal.md) | External | High‑level introduction to Chronos. |

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
| **Self‑Update** | Automatic binary updates via Cloud heartbeat. |
| **Behavior Logging** | Sparse behaviour recording for RL training with MessagePack + GZip compression. |
| **Performance** | Memory‑mapped files, pooled arrays, allocation‑free hot paths. |

---

## Versioning

| Version | Status | Description |
|---------|--------|-------------|
| **1.0.0 LTS** | Current | Stable release with hooks‑and‑slots extension system, GA optimisation, live trading, Cloud connectivity, and 60+ commands. |
| **1.5.0** | Planned | Enhanced reporting, performance improvements, additional hook points. |
| **2.0.0 LTS** | Planned | Multi‑operation engine, advanced marketplace, ONNX and RL model support. |

Chronos follows [Semantic Versioning](https://semver.org). Each major version is an LTS release.

---

## Command ID Registry

All 60+ commands are fully implemented in `Chronos.Core.Engine.Management.Commands.Handlers`.

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
| `Chronos.Core.Sdk` | Proprietary, freely redistributable for extension development. |
| `Chronos.Core.Shared` | Closed‑source, all rights reserved. |
| `Chronos.Core.Kernel` | Closed‑source, all rights reserved. |
| `Chronos.Core.Engine` | Closed‑source, distributed as part of the Chronos Engine binary. |

---

## Contributing

This repository is closed‑source. Contribution is restricted to Chronos core team members.

For extension development, please refer to the [Extension Developer Guide](core/docs/Chronos%20Extension%20Developer%20Guide.md).

---

## Support

- **Documentation:** See the `docs/` directory.
- **Issues:** Contact Chronos support through the Cloud dashboard.
- **Community:** Visit the Chronos developer forum (coming soon).

---

*This README is the authoritative entry point for the Chronos.Core solution. All code, documentation, and design decisions must align with the [Chronos Principles](core/docs/Chronos%20Principles.md).*