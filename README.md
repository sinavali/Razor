# Chronos Core

**Institutional‑grade algorithmic trading engine – v1.0.0 LTS**

The `core` repository contains the heart of the Chronos ecosystem: the public SDK for extension developers, the closed‑source engine that executes backtests, optimizations, and live trading, and the headless engine executable that connects to Chronos Cloud.

## Architecture

```
┌──────────────────────────────────────────────┐
│                  Chronos Cloud               │  (SaaS – management & monitoring)
└──────────────────┬───────────────────────────┘
│ encrypted WebSocket
┌──────────────────▼───────────────────────────┐
│              Chronos Engine                   │  (headless binary)
└──────────────────┬───────────────────────────┘
│
┌──────────────────▼───────────────────────────┐
│          Chronos.Core.Kernel                  │  (closed‑source core)
│  backtesting • optimisation • live trading    │
│  genetic algorithm • telemetry • hooks        │
└──────────────────┬───────────────────────────┘
│ references
┌──────────────────▼───────────────────────────┐
│       Chronos.Core.Abstractions               │  (public NuGet SDK)
│  hooks • slots • domain types • utilities     │
└──────────────────────────────────────────────┘
```

## Projects

| Project | Description | Visibility |
|---------|-------------|------------|
| `Chronos.Core.Abstractions` | Public SDK for building extensions (adapters, strategies, indicators, hook plugins, NN models) | NuGet package |
| `Chronos.Core.Kernel` | Closed‑source engine implementing all trading logic | Private |
| `Chronos.Core.Engine` | Headless executable that hosts the Kernel and communicates with Chronos Cloud | Private |

## Quick Start – Extension Developers

1. Install the `Chronos.Core.Abstractions` NuGet package.
2. Implement one or more contracts (`IHookManifest`, `IStrategyCapability`, `IAdapterCapability`, `INeuralNetworkModel`, or `Indicator`).
3. Add the SDK version attribute to your assembly:
   ```csharp
   [assembly: ChronosSdkVersion("1.0.0")]
   ```
4. Build your DLL and place it in the appropriate engine directory (`Adapters/`, `Strategies/`, `Indicators/`, `Plugins/`, or `NeuralNetworks/`).
5. Manage activation via Chronos Cloud.

## Quick Start – Core Developers

### Prerequisites
- .NET 10 SDK (`10.0.300` or later, see `global.json`)

### Build
```bash
cd core
dotnet restore
dotnet build --configuration Release
```

### Test
```bash
dotnet test --configuration Release
```

## Documentation

- [Chronos Principles](./docs/Chronos%20Principles.md)
- [Configuration Reference](./docs/Chronos%20Configuration%20Reference.md)
- [Plugin Developer Guide](./docs/Chronos%20Plugin%20Developer%20Guide.md)

## Licensing

- `Chronos.Core.Abstractions` – Proprietary, freely redistributable.
- `Chronos.Core.Kernel` – Closed‑source, all rights reserved.
- `Chronos.Core.Engine` – Closed‑source, distributed as part of the Chronos Engine binary.
