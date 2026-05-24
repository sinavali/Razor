## Chronos Principles — v1.0.0 LTS

### Purpose
This document defines the immutable, mandatory design rules that govern the Chronos trading engine. Every line of code committed to the `Chronos.Core.Abstractions` and `Chronos.Core.Kernel` projects (and future `Chronos.Engine` and `Chronos.Cloud` projects) must respect these principles. They apply to the current v1.0.0 LTS release and all future versions unless explicitly superseded by a later constitution.

These principles are not implementation details; they are the **architectural contract**. All subsystems – backtesting, live trading, optimisation, reporting, plugin loading – derive from them.

---

## 1. Market / Exchange / Asset Agnosticism
**Chronos must work with definitions, never with hard‑coded special cases.**

- Chronos itself must have **zero knowledge** of the underlying market type (Forex, Crypto, Futures, CFD, etc.).
- All exchange‑specific behaviour – order types, funding mechanisms, tick sizes, contract sizes, margin modes, PnL formulas, naming conventions – is delegated to **adapter plug‑ins** through clean interfaces (`IAdapter`, `IMarketCalculator`, etc.).
- Adapters are the sole authoritative source of market logic. The core engine never performs an `if (assetClass == …)` check.
- New markets are added exclusively by creating a new adapter; no engine changes are permitted.

---

## 2. Determinism is Mandatory
**Given the same inputs, Chronos must produce bit‑identical output on every run, across all supported environments, within the same .NET major version (and across versions when a portable RNG is used).**

- All randomness in backtesting, optimisation, and gene initialisation must be seeded deterministically from a user‑supplied master seed.
- `System.Random` may be used **only** when perfect cross‑version reproducibility is not required. For GA operations that must be auditable across .NET runtime updates, a custom portable pseudo‑random number generator (`ChronosRandom`) is required. This generator must be serialisable so that optimisation state can be saved and resumed exactly.
- The GA’s individual seed generation must not rely on `HashCode` (which changes between .NET versions). Instead, use the master seed plus a deterministic, stable hash (e.g., a pre‑generated sequence from the portable RNG).
- System clock (`DateTime.UtcNow`, `Environment.TickCount64`, etc.) must never influence trading or backtest logic. (See Principle 3.)
- A “golden test” suite must exist that executes deterministic scenarios and fails if the output hash changes.
- Determinism is non‑negotiable: if it breaks, Chronos is unusable for strategy validation.

---

## 3. Internal Clock – No System Time in Trading Logic
**All market‑time calculations (trade timestamps, holding costs, daily drawdown resets, window completion, order execution time, etc.) must be driven solely by the timestamp of the latest tick.**
System time may be used **only** for non‑trading concerns: scheduling, health checks, logging, telemetry, and external timeouts.

- Introduce an `IClock` abstraction with two implementations:
  - `TickClock` – returns the timestamp of the last tick processed (monotonic within a backtest or tick stream).
  - `SystemClock` – returns `DateTime.UtcNow` and `TickCount64` for wall‑clock operations.
- Brokers (simulated and live) must receive a clock instance. All PnL updates, holding cost calculations, and daily stats resets must use the `TickClock` time.
- Order timeouts (in‑flight guards) must use a monotonic clock (e.g., `TickCount64` from `SystemClock`) to avoid system time adjustments.
- The live pipeline’s tick handler must feed the tick timestamp to the `TickClock` before any processing.
- Any code that calls `DateTime.UtcNow` inside a trading path is a violation.

---

## 4. Tick‑Only Core — No Bar Dependencies
**Chronos works exclusively with tick data. Bars (OHLCV) are legacy artifacts and must never appear in core processing.**

- The `Tick` struct is the fundamental unit of price information.
- Strategies receive ticks via `OnTickAsync(Tick tick)`. There is no `OnBarAsync` in the `IStrategy` interface.
- Aggregated views (OHLC for a timeframe) are provided on‑demand by `TickWindow`, which computes them from raw ticks. Strategies that do not need aggregated data never pay the cost.
- Bar‑to‑tick conversion (`BarsToTicks`) exists solely in the adapter layer for importing legacy data; it must not leak into the core engine.
- All internal indicators, risk models, and trade signals must be designed to consume tick streams directly.

---

## 5. Live‑Backtest Behavioural Parity
**The simulated broker and live broker must produce identical market‑to‑account effects for the same tick sequence.**
Any divergence is a bug.

- Both brokers must use the same `IMarketCalculator` implementation for margin, PnL, commission, swap/funding, and holding cost.
- The order execution pipeline (market/limit/stop orders, SL/TP monitoring, stop‑out logic) must be structurally identical between sim and live.
- The live broker may contain additional reconciliation and reconnection logic, but never alternative trading math.
- Unit tests must compare sim‑vs‑live broker outputs for equivalent synthetic tick feeds.

---

## 6. Plug‑in Versioning & Compatibility
**All plug‑gable components must carry explicit version information that Chronos validates before loading.**

- Every plug‑in assembly must declare the version of the Chronos SDK it targets (e.g., `ChronosSdkVersion("1.0.0")`) and its own release version for diagnostics.
- A version manager in Chronos must inspect the declared version and check compatibility:
  - Plug‑ins written for an older major version may be loaded if backward compatibility is guaranteed and they pass an interface validation (e.g., missing members are handled).
  - Plug‑ins targeting a newer major version are rejected unless an explicit compatibility mode is configured.
- The SDK (contracts assembly) itself is versioned strictly; breaking changes require a major version bump.
- Future feature‑based versioning (e.g., adapter‑level capabilities) will be expressed through dedicated version attributes, allowing the host to negotiate features without breaking older plug‑ins.

---

## 7. Adapter‑Owned Data Lifecycle
**The adapter creates binary data files; the adapter is responsible for deleting them – but only when Chronos signals it is safe.**

- The `IHistoricalDataProvider` interface exposes `NotifyFileSafeToDeleteAsync(string filePath)`. Chronos calls this after it has finished reading the file (memory‑mapped view released).
- The adapter must not delete a file until it receives this notification.
- The `BorrowedTickData` disposable wrapper in the SDK guarantees this call on disposal.
- File management is entirely the adapter’s concern; Chronos never touches the file system directly for data files.

---

## 8. Sorted Tick Data Contract
**Adapters must guarantee that ticks stored in binary files are sorted by ascending time.**
Chronos relies on this invariant for efficient merging and window computation.

- `BinaryDataMapper.WriteTicksToBinary` and the memory‑mapped reader assume sorted data.
- A debug‑only assertion must verify sorted order when opening a file; unsorted data is an adapter error and must throw `AdapterException`.
- Adapters that synthesise ticks from bars are responsible for producing a correctly sorted array.
- Live tick streams are not guaranteed sorted, but the pipeline merges them chronologically; this principle applies only to historical data.

---

## 9. Configuration is Source of Truth – Strict Validation
**Every configuration object (specification record) must be the final, validated, immutable source of truth.**

- All specification records implement `Validate()` that throws `ConfigurationException` on invalid values.
- Configuration loaders must **not** silently default critical parameters (e.g., leverage, stop‑out level). Sensitive fields must be explicitly set by the user or omitted entirely (causing a validation error).
- Sensible defaults may exist only for non‑critical parameters (e.g., thread count, file retention policy) and must be clearly documented.
- Parsing of user input (e.g., timeframe strings) must use `TryParse` and provide clear error messages.
- Validation must be exhaustive: every field that affects trading behaviour is checked.

---

## 10. Performance & Memory Efficiency
**Chronos must be fast and memory‑efficient; resource waste is unacceptable in an institutional engine.**

- Favour stack allocation, `Span<T>`, `ArrayPool`, `GC.AllocateUninitializedArray`, and blittable structs wherever possible.
- The hot tick‑processing path (backtest loop, live tick handler) must minimise allocations. Zero‑allocation merging and indicator calculation are a goal.
- Memory‑mapped files are used for historical data to avoid loading large arrays into managed memory.
- `TickRingBuffer` and `Indicator` base class use pooled arrays; they must return buffers on disposal.
- No lazy allocations in performance‑critical sections; all required buffers are pre‑allocated on strategy start.
- The genetic optimiser evaluates chromosomes in parallel; parallelism must respect a user‑configurable thread limit, not blindly consume all cores.

---

## 11. Code Quality & Documentation Mandates
**Code is the ultimate specification; it must be self‑documenting, strictly styled, and rigorously reviewed.**

- Every public and protected member must have XML documentation comments (enforced by `CS1591` as error).
- `.editorconfig` enforces consistent style; intentional suppressions (`#pragma warning disable`) must be scoped and justified in a comment.
- Suppressed warnings like `CA5394` (non‑secure Random) are allowed within the deterministic‑seeded GA context but must be accompanied by a `// Reason:` comment.
- All async methods that accept `CancellationToken` must forward it to all downstream operations.
- Dead code, unused variables, and obsolete patterns must be removed before release.

---

## 12. Testing & Determinism Verification
**Chronos must be accompanied by a comprehensive test suite that proves correctness, stability, and determinism.**

- Unit tests must cover all public interfaces, edge cases, and mathematical calculations (margin, drawdown, Sharpe ratio).
- Integration tests must run full pipelines (backtest, walk‑forward, simulated live) with mock adapters and known datasets.
- The golden‑file determinism test must execute a full backtest twice and compare the hash of the trade history (or serialised result) – any mismatch fails CI.
- All new features must include tests that pass before merge.

---

## 13. Live Trading Robustness
**The live engine must never silently fail or desynchronise from the exchange.**

- The live broker must periodically reconcile account state (balance, positions, orders) with the adapter, independent of ticks.
- After a connection loss and successful reconnection, a full state reconciliation is mandatory before processing further ticks.
- Adapter implementations must be thread‑safe, as they may be called from multiple threads concurrently (tick events, timer sync, command execution). This requirement is documented on the `IAdapter` contract.
- In‑flight order guards prevent duplicate submission; they must use monotonic time, not wall‑clock time.
- All exceptions in the live tick callback must be caught, logged, and must never crash the process. Fire‑and‑forget operations must observe exceptions via a fault logger.
- Health checks must report connectivity, last tick age, and optimisation status; they must be instance‑based (not global static) to support future multi‑engine deployments in separate processes.

---

## 14. Plugin Isolation & Future Extensibility
**Chronos must load plug‑in assemblies in isolated contexts to ensure stability and security.**

- Adaptor DLLs are loaded via a dedicated `AssemblyLoadContext` that resolves dependencies independently.
- Assemblies must be signed; unsigned plug‑ins are rejected in production mode.
- The plugin loading mechanism must discover adapters by the `[AdapterName]` attribute and validate the target Chronos version before instantiation.
- This isolation prepares Chronos for future versions where additional component types will also be pluggable via the same versioned loading system.

---

## 15. Messaging & Observability
**All significant events (order execution, connection state, optimisation progress) must be published through a lightweight in‑process message bus, enabling decoupled monitoring and logging.**

- The `IMessageBus` must support typed subscriptions and deduplication (via `EventId`).
- Event payloads must contain all relevant metrics (e.g., `BacktestCompletedEvent` includes Sharpe ratio, profit factor) so dashboards can display key statistics without parsing reports.
- Chronos must expose OpenTelemetry metrics (histograms, counters) for backtest throughput, GA fitness improvement, live order latency, and live tick latency.
- Connection state must be exposed as a gauge for health check integration.

---

## 16. Future‑Proofing & .NET Version
**Chronos must always target the latest stable major version of .NET available at the time of its own major release.**
For the current LTS release, that is .NET 10 (as defined in the repository’s `global.json` and project files); future LTS releases will adopt the latest stable major version available.

- All NuGet dependencies must be updated to their latest **stable** (non‑preview) releases. Security patches and bug‑fix updates are applied promptly, independent of the Chronos release cycle.
- New C# features (e.g., `params ReadOnlySpan<T>`, improved pattern matching, `field` keyword) may be adopted when they improve clarity or performance.
- Reflection is permitted for plugin loading and gene injection, as AOT compilation is incompatible with dynamic assembly loading.
- The architecture must remain cleanly layered so that future runtime upgrades require minimal code changes.

---

## 17. Versioning & Long‑Term Support
**Each major release of Chronos (1.x, 2.x, …) is an LTS release.**
Breaking changes are reserved for major version boundaries; minor and patch releases must preserve determinism, adapter compatibility, and API stability within the same major version.

- **Major (X.0.0):** may introduce breaking changes to public APIs, plugin contracts, or engine behaviour.
- **Minor (X.Y.0):** may add non‑breaking features, new interfaces, or new configuration options. Must not alter existing backtest determinism or break adapter loading.
- **Patch (X.Y.Z):** reserved for critical bug fixes and security patches only.
- Any change that could alter the output of a golden‑test determinism scenario is a **breaking change** and requires a major version increment.

---

## 18. Feature‑Based Versioning (Future)
**When multiple types of plug‑ins are introduced in later major versions, a feature‑based versioning system must be used to negotiate capabilities between the host and plug‑ins.**
This will be expressed via version attributes (e.g., `[ChronosFeature("FeatureName", Version)]`) that allow the host to enable or restrict functionality based on plug‑in compatibility. The design must ensure that older adapters remain loadable without recompilation when the host’s major version advances, provided they declare their targeted SDK version and pass interface validation.

---

## 19. Principle Precedence
When conflicts arise between these principles, the following precedence order applies (higher number overrides lower when absolutely necessary):

1. Determinism (Principle 2)
2. Tick‑Only Core (Principle 4)
3. Internal Clock (Principle 3)
4. Live‑Backtest Parity (Principle 5)
5. Market Agnosticism (Principle 1)
6. Configuration Validation (Principle 9)
7. Sorted Data Contract (Principle 8)
8. All others.

**No principle can be violated without an explicit, documented exception approved by the Chronos architecture board.**

---

## 20. Enforcement
- Pull requests that contradict these principles are rejected.
- A static analysis step in CI verifies adherence (where automatable, e.g., no `DateTime.UtcNow` in `Chronos.Core.Kernel/Brokers`, no `if (assetClass...)` in core).
- The golden determinism test is a CI gate.
- Adapter developers receive a separate SDK guide derived from this constitution, outlining the mandatory contracts they must honour.

---

*This document is living; it may be amended only with a formal review. Amendments must be backward‑compatible with existing adapters and strategies unless a major version increment occurs.*
