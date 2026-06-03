# Chronos.Core.Kernel

**Closed‑source core trading engine – v1.0.0 LTS**  
**Audience:** Internal Chronos developers only  
**Status:** Private – do not distribute

---

## Purpose

`Chronos.Core.Kernel` is the implementation heart of the Chronos trading system. It contains all runtime logic:

- **Backtesting engine** – Deterministic tick‑by‑tick replay with full state isolation.
- **Genetic optimiser** – Population‑based optimisation with hyper‑mutation and state serialisation.
- **Live trading broker** – Exchange‑connected broker with reconciliation, order guarding, and telemetry.
- **Simulated broker** – Friction‑aware, latency‑simulating broker for backtesting and optimisation.
- **Telemetry & observability** – OpenTelemetry metrics for GA progress, backtest throughput, live order latency, and connection state.
- **Plugin loading** – Isolated `AssemblyLoadContext` for adapters, strategies, and all other plugin types.
- **Configuration system** – Immutable specifications with exhaustive `Validate()` methods.

It depends on `Chronos.Core.Abstractions` (the public SDK) and is consumed by the future `Chronos.Engine` executable.

---

## Project Structure

```
Chronos.Core.Kernel/
├── Backtesting/
│   ├── BacktestInput.cs          ← Immutable input record
│   ├── BacktestProgress.cs       ← Progress DTO
│   ├── BacktestResult.cs         ← Result DTO
│   ├── BacktestRunner.cs         ← Orchestrator (IBacktestRunner)
│   ├── IBacktestRunner.cs        ← Runner interface
│   └── MergedTickTimeline.cs     ← Multi‑stream chronological merge
├── Brokers/
│   ├── LiveBroker.cs             ← Exchange‑connected broker
│   ├── SimulatedBroker.cs        ← Deterministic simulation broker
│   └── ...                       ← (internal helpers)
├── Clock/
│   ├── IClock.cs                 ← Time abstraction
│   ├── SystemClock.cs            ← Wall‑clock (TickCount64)
│   └── TickClock.cs              ← Tick‑driven clock
├── Configuration/
│   ├── ExecutionSpecification.cs ← Backtest/optimisation parameters
│   ├── LiveSpecification.cs      ← Live trading parameters
│   └── OptimizationSpecification.cs ← GA parameters
├── Indicators/
│   ├── IndicatorRegistry.cs      ← Caching indicator factory
│   └── IndicatorRegistryFactory.cs
├── Messaging/
│   └── MessageBus.cs             ← In‑process typed pub/sub
├── Metrics/
│   ├── FitnessCalculator.cs
│   ├── IMetricsCalculator.cs
│   ├── MetricsCalculator.cs
│   └── SummaryMetrics.cs
├── Optimization/
│   ├── Chromosome.cs             ← Candidate solution
│   ├── GeneticOptimizer.cs       ← Steppable GA engine
│   ├── GeneticOptimizerState.cs  ← Pause/resume snapshot
│   ├── IGeneticOptimizer.cs
│   ├── IOptimizer.cs
│   └── OptimizationProgress.cs
├── Plugins/
│   ├── AdapterFactory.cs         ← Discovers IAdapter by [AdapterName]
│   ├── PluginLoadContext.cs      ← Isolated assembly loading
│   ├── PluginRegistry.cs         ← Generic plugin discovery
│   └── PluginValidator.cs        ← SDK version validation
├── Telemetry/
│   └── ChronosMetrics.cs         ← OpenTelemetry metrics
└── Chronos.Core.Kernel.csproj
```

---

## Building

### Prerequisites

- .NET 10 SDK (`10.0.300` or later – see `global.json`)
- Access to the Chronos NuGet feed (local Gitea instance or package cache)
- Strong‑name key for signing (provided via environment/secrets, not committed)

### Restore & Build

```bash
cd core
dotnet restore
dotnet build --configuration Release
```

For local development where you want the Kernel to reference the local `Abstractions` project instead of the NuGet package, set the property `UseLocalAbstractions=true` in your `Directory.Build.props.user` (never commit this file):

```xml
<!-- Directory.Build.props.user (git‑ignored) -->
<Project>
    <PropertyGroup>
        <UseLocalAbstractions>true</UseLocalAbstractions>
    </PropertyGroup>
</Project>
```

When packing for distribution, `UseLocalAbstractions` must be `false` so that the produced `.nupkg` declares the correct NuGet dependency on `Chronos.Core.Abstractions`.

### Pack

```bash
dotnet pack src/Chronos.Core.Kernel/Chronos.Core.Kernel.csproj \
    --configuration Release \
    -p:UseLocalAbstractions=false \
    -p:PackageVersion=1.0.0 \
    --output ./nupkgs
```

---

## Testing

### Unit & Integration Tests

```bash
dotnet test --configuration Release --verbosity normal
```

Test projects are not yet in the solution – they will be added as part of the v1.0.0 release testing gate.  
The test suite will include:

- **Unit tests** for all brokers, calculators, and metrics.
- **Integration tests** for full backtest and GA pipelines.
- **Live‑backtest parity tests** – feed identical ticks to `SimulatedBroker` and `LiveBroker` (with mock adapter); compare trade histories.
- **Golden determinism tests** (`Chronos.Determinism.Tests`) – run identical backtests twice and compare trade history hashes.

### Determinism Gate

The determinism golden test is a **mandatory CI gate**. Any change that alters the trade history hash of a standard backtest scenario is a breaking change and requires a major version bump.

---

## Key Design Decisions

- **Determinism above all** – No `DateTime.UtcNow` or `System.Random` in trading paths. All randomness is seeded from user‑supplied master seeds via `ChronosRandom` (xorshift128+).
- **Tick‑only core** – Bars (OHLCV) only exist in the adapter layer. Strategies receive raw ticks; aggregated views are computed on‑demand by `TickWindow`.
- **Live‑backtest parity** – Both brokers use the same `IMarketCalculator`, same stop‑out logic, same SL/TP evaluation, and same holding cost calculations.
- **Immutable specifications** – Every configuration record has a `Validate()` method; no silent defaults for critical parameters.
- **Plugin isolation** – All third‑party code is loaded in its own `AssemblyLoadContext` with independent dependency resolution.

See the full [Chronos Principles](../../docs/Chronos%20Principles.md) for the non‑negotiable architectural rules.

---

## Contributing

1. Read the [Chronos Principles](../../docs/Chronos%20Principles.md) and the [Internal Architecture Document](../../docs/Chronos%20Internal%20Technical%20Architecture%20Document%20(v1.0.0%20LTS).md).
2. All public and protected members must have XML documentation (enforced by `CS1591` as error).
3. Run `dotnet format` before committing to enforce code style.
4. The determinism golden test must pass before merging.
5. Any change that could affect backtest output or plugin compatibility requires an architecture board review.

---

## Contact

For questions about the Kernel codebase, contact the Chronos architecture board.  
Do not share this code or documentation outside the internal development team.