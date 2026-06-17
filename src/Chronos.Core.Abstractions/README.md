# Chronos.Core.Abstractions

**Public SDK for Chronos extension development**
Version: 1.0.0 LTS

This package contains only contracts – interfaces, records, enums, and utilities – with **no runtime logic**.
It is the only dependency you need to build extensions for Chronos (adapters, strategies, indicators, hook plugins, and neural network models).

## Architecture

Chronos extensions are organized into three concepts:

| Concept | What It Does | Directory |
|---------|-------------|-----------|
| **Slots** | Required capabilities (Adapter, Strategy, NN Model) | `Adapters/`, `Strategies/`, `NeuralNetworks/` |
| **Indicators** | Technical analysis computations | `Indicators/` |
| **Hooks** | Intercept and observe engine events | `Plugins/` (any scanned directory) |

A single DLL can combine any of these – for example, a strategy that also registers hooks.

## Hook System

Hooks are the primary extensibility mechanism. A hook plugin implements `IHookManifest` and registers callbacks on named hook points with priorities:

- **Filter hooks** – transform or reject data flowing through the pipeline (e.g., order validation).
- **Action hooks** – observe events without modifying data (e.g., send a notification).

Hook callbacks are strongly typed. Action hook callbacks **must be synchronous** – do not use `async void`; exceptions inside async void crash the process. See the Extension Developer Guide for safe async patterns.

See the `Chronos.Core.Abstractions.Hooks` namespace for the full catalog.

## Slot Capabilities

| Slot | Interface | Description |
|------|-----------|-------------|
| Adapter | `IAdapterCapability` | Connects to a broker/exchange |
| Strategy | `IStrategyCapability` | Trading logic – `OnTick(string symbol, Tick tick)` |
| Neural Network | `INeuralNetworkModel` | Feed‑forward, ONNX, LSTM, RL models |

## Documentation

- [Chronos Principles](../../docs/Chronos%20Principles.md)
- [Configuration Reference](../../docs/Chronos%20Configuration%20Reference.md)

## Quick Start

1. Create a .NET class library targeting `net10.0`.
2. Add this package:
   ```xml
   <PackageReference Include="Chronos.Core.Abstractions" Version="1.0.0" />
   ```
3. Add the SDK version attribute:
   ```csharp
   [assembly: ChronosSdkVersion("1.0.0")]
   ```
4. Implement one or more contracts (`IHookManifest`, `IStrategyCapability`, etc.).
5. Build and place the DLL in the appropriate engine directory.

## Versioning

This package follows [Semantic Versioning](https://semver.org).
Breaking changes only occur in major releases.

## License

Proprietary. Redistribution allowed.