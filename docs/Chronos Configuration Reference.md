## Chronos Configuration Reference

**Version:** 1.0.0 LTS  
**Audience:** Extension developers & power users  
**Status:** Authoritative  
**Last Updated:** 2026-07-07  

---

### Purpose

This document catalogues every configuration object, enumeration, and data contract that controls Chronos engine behaviour. It is the single source of truth for parameter names, types, validation rules, and acceptable values. Use it when writing strategies, building adapters, or interpreting Cloud‑supplied payloads.

---

## 1. Strategy Specification

**Type:** `StrategySpecification` (immutable record)  
**Namespace:** `Chronos.Core.Abstractions.Shared`

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `InitialBalance` | `double` | Yes | Starting account balance in quote currency. |
| `Leverage` | `double` | Yes | Account‑wide leverage multiplier (e.g., 10, 50). Must be `> 0`. |
| `RequestedSymbols` | `ImmutableArray<SymbolRequest>` | Yes | List of symbols and their timeframes the strategy needs. At least one element required. Symbols must be unique (case‑insensitive). |

### Nested Type: `SymbolRequest`

| Field | Type | Description |
|-------|------|-------------|
| `Symbol` | `string` | Broker symbol (e.g., `"EURUSD"`, `"BTCUSDT"`). |
| `TimeFrames` | `ImmutableArray<TimeFrame>` | Timeframes the strategy consumes. Can include `Tick` if raw tick feed is desired. |

### Validation

- `InitialBalance` must be `> 0`.
- `Leverage` must be `> 0`.
- `RequestedSymbols` must not be empty.
- Each `SymbolRequest` must have a non‑empty `Symbol` and at least one `TimeFrame` (but `TimeFrame.Tick` alone is valid).
- Duplicate `Symbol` values (case‑insensitive) are not allowed. A `ConfigurationException` is thrown if the same symbol appears more than once.

### Example (JSON, as Cloud might send)

```json
{
    "InitialBalance": 10000.0,
    "Leverage": 100.0,
    "RequestedSymbols": [
        {
            "Symbol": "EURUSD",
            "TimeFrames": ["M1", "H1"]
        },
        {
            "Symbol": "BTCUSDT",
            "TimeFrames": ["Tick"]
        }
    ]
}
```

---

## 2. Execution Specification

**Type:** `ExecutionSpecification` (immutable record)  
**Namespace:** `Chronos.Core.Kernel.Configuration`

Controls the backtest or optimisation run environment.

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `StartDate` | `DateTime` | Yes | — | First tick timestamp (UTC, inclusive). |
| `EndDate` | `DateTime` | Yes | — | Last tick timestamp (UTC, inclusive). Must be `> StartDate`. |
| `MaxParallelThreads` | `int` | No | `0` (auto) | Maximum threads for GA evaluation. `0` = `Environment.ProcessorCount - 1`. |
| `LatencyTicks` | `long` | No | `0` | Simulated execution delay in 100‑ns ticks. `0` = instant fill. |
| `WarmupWindowCount` | `int` | No | `0` | Number of initial tick windows to skip for signal generation. |
| `MaxOpenPositions` | `int` | Yes | — | Hard limit on concurrent positions. Must be `> 0`. |
| `StopOutLevel` | `double` | Yes | — | Stop‑out margin ratio (e.g., `0.5` = 50%). Must be `> 0` and `≤ 1`. |
| `GeneInitializationSeed` | `int?` | No | `null` | Seed for deterministic gene initialization when no explicit genes are provided. `null` means no seed was explicitly supplied. |

> **Note:** The `ExecutionSpecification` does **not** contain a `HistoricalDataPolicy` field. Data retention is controlled at the `BorrowedTickData` level via the `DataActionPolicy` parameter passed to its constructor. The `DataActionPolicy` enum values are `KeepUntilExit`, `DeleteAfterTask`, and `PersistentCache`.

### Validation

- `EndDate > StartDate`.
- `WarmupWindowCount ≥ 0`.
- `MaxOpenPositions > 0`.
- `0 < StopOutLevel ≤ 1`.
- `MaxParallelThreads ≥ 0`.

### Example

```json
{
    "StartDate": "2024-01-01T00:00:00Z",
    "EndDate": "2024-12-31T23:59:59Z",
    "WarmupWindowCount": 100,
    "MaxOpenPositions": 5,
    "StopOutLevel": 0.5,
    "LatencyTicks": 0,
    "MaxParallelThreads": 0,
    "GeneInitializationSeed": 12345
}
```

---

## 3. Optimization Specification

**Type:** `OptimizationSpecification` (immutable record)  
**Namespace:** `Chronos.Core.Kernel.Configuration`

The optimisation pipeline uses the hook system for fitness evaluation. No `FitnessModel` field is present in the specification; instead, the engine invokes the `optimization.chromosome.evaluated` hook after each chromosome evaluation, and the Cloud or a hook plugin computes the fitness score.

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MasterSeed` | `int` | Yes | — | Master seed for the entire GA run. Guarantees reproducibility. Must be non‑negative. |
| `Generations` | `int` | Yes | — | Number of generations to evolve. Must be `> 0`. |
| `PopulationSize` | `int` | Yes | — | Individuals per generation. Must be `≥ 4`. |
| `MutationRate` | `double` | Yes | — | Base probability of gene mutation. `[0, 1]`. |
| `CrossoverRate` | `double` | Yes | — | Probability that a gene comes from the first parent. `[0, 1]`. |
| `ElitismPct` | `double` | Yes | — | Fraction of best individuals preserved unchanged. `[0, 1]`. |
| `TournamentSize` | `int` | Yes | — | Number of individuals competing in selection. Must be `≥ 2`. |
| `StagnationGenerationsBeforeHyper` | `int` | No | `3` | Generations without improvement before hyper‑mutation activates. `≥ 1`. |

### Validation

- `Generations > 0`, `PopulationSize ≥ 4`.
- `0 ≤ MutationRate ≤ 1`, `0 ≤ CrossoverRate ≤ 1`, `0 ≤ ElitismPct ≤ 1`.
- `TournamentSize ≥ 2`.
- `StagnationGenerationsBeforeHyper ≥ 1`.
- `MasterSeed` must be `≥ 0`. Negative seeds are rejected to guarantee deterministic and predictable sequences.

### Example

```json
{
    "MasterSeed": 42,
    "Generations": 50,
    "PopulationSize": 100,
    "MutationRate": 0.1,
    "CrossoverRate": 0.5,
    "ElitismPct": 0.05,
    "TournamentSize": 3,
    "StagnationGenerationsBeforeHyper": 3
}
```

---

## 4. Live Specification

**Type:** `LiveSpecification` (immutable record)  
**Namespace:** `Chronos.Core.Kernel.Configuration`

The live trading specification defines the parameters for a live trading session. Continuous optimisation is orchestrated by Chronos Cloud; the Cloud sends the engine commands to start/stop optimisation runs based on the user’s profile settings.

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MagicNumber` | `int` | Yes | — | Unique number to tag orders from this strategy instance. Must be `> 0`. |
| `OrderGuardTimeoutSeconds` | `int` | No | `5` | Window (in seconds) during which duplicate orders are rejected. Must be `> 0`. |

### Validation

- `MagicNumber > 0`.
- `OrderGuardTimeoutSeconds > 0`.

### Example

```json
{
    "MagicNumber": 123456,
    "OrderGuardTimeoutSeconds": 10
}
```

---

## 5. Neural Network Model Interface

**Type:** `INeuralNetworkModel` (interface)  
**Namespace:** `Chronos.Core.Abstractions.Slots`

Neural networks are slot capabilities. The engine activates an `INeuralNetworkModel` instance from the `NeuralNetworks/` directory when the active strategy declares `RequiresNeuralNetwork = true`. The model supports feed‑forward, ONNX, LSTM, RL, and other architectures through a unified parameter‑vector interface compatible with the GA.

### Members

| Member | Type | Description |
|--------|------|-------------|
| `ModelType` | `string` | Human‑readable model type identifier (e.g., `"FeedForward"`, `"ONNX"`, `"RL-DQN"`). |
| `InputSize` | `int` | Number of input features the model expects. |
| `OutputSize` | `int` | Number of output values the model produces. |
| `ParameterCount` | `int` | Total number of double parameters (weights, biases, etc.) that the GA includes in the chromosome. |
| `Predict` | `double[] Predict(double[] inputs)` | Performs a forward pass and returns predictions. |
| `LoadParameters` | `void LoadParameters(double[] genes)` | Loads a flat parameter vector into the model's internal structure. |
| `ExportParameters` | `double[] ExportParameters()` | Exports the current parameters as a flat array. |
| `Reset` | `void Reset()` | Resets any internal state. Called before each backtest or evaluation run. |
| `SerializeState` | `byte[] SerializeState()` | Serializes the full model state for save/restore. |
| `DeserializeState` | `void DeserializeState(byte[] state)` | Deserializes the model state from a previously saved snapshot. |

### Strategy Integration

The `IStrategyCapability` interface (in `Chronos.Core.Abstractions.Slots`) exposes:

| Member | Type | Description |
|--------|------|-------------|
| `RequiresNeuralNetwork` | `bool` | Whether this strategy requires a neural network model. |
| `NeuralNetwork` | `INeuralNetworkModel?` | The neural network model, set by the engine before initialization. |
| `TotalGeneCount` | `int` | Total genes including strategy properties and neural network parameters. |

---

## 6. Symbol Properties

**Type:** `SymbolProperties` (record)  
**Namespace:** `Chronos.Core.Abstractions.Shared`

Adapters return this object per symbol; it defines exchange‑specific contract details. **All fields are required.** Adapters must explicitly set every property.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `AssetClass` | `AssetClass` | Market category (Forex, CryptoSpot, Equity, …). |
| `MarginMode` | `MarginMode` | Cross or Isolated margin. |
| `PendingTrigger` | `PendingOrderTriggerMode` | Which price triggers pending buy orders. |
| `MarginCurrency` | `string` | Currency for margin calculations. |
| `ContractSize` | `double` | Size of one standard contract. |
| `TickSize` | `double` | Minimum price increment. |
| `TickValue` | `double` | Monetary value of one tick. |
| `MinVolume` | `double` | Minimum order volume. |
| `MaxLeverage` | `double` | Maximum allowed leverage for this symbol. |
| `SwapLong` | `double` | Daily swap rate for long positions (percentage or absolute). |
| `SwapShort` | `double` | Daily swap rate for short positions. |
| `SwapRolloverHourUtc` | `int` | UTC hour at which swap is charged. |
| `TripleSwapDayMultiplier` | `double` | Multiplier for triple‑swap days (typically Wednesdays). |
| `FundingRate` | `double` | Perpetual contract funding rate (per period). |
| `InitialMarginRate` | `double` | Fraction of position value required as initial margin. |
| `MaintenanceMarginRate` | `double` | Fraction below which liquidation may occur. |
| `MakerFeeRate` | `double` | Fee for maker orders. |
| `TakerFeeRate` | `double` | Fee for taker orders. |

### Enums Used

- `AssetClass`: `Forex`, `CryptoSpot`, `CryptoPerpetual`, `Equity`, `Future`, `CFD`
- `MarginMode`: `Cross`, `Isolated`
- `PendingOrderTriggerMode`: `UseBidForBuy`, `UseAskForBuy`, `UseMidPrice`

---

## 7. TimeFrame

**Type:** `TimeFrame` (enum)  
**Namespace:** `Chronos.Core.Abstractions.Shared`

The integer value equals the duration in minutes.

| Member | Value (min) | Description |
|--------|-------------|-------------|
| `Tick` | `0` | No aggregation; raw tick stream. |
| `M1` | `1` | 1 minute. |
| `M5` | `5` | 5 minutes. |
| `M15` | `15` | 15 minutes. |
| `M30` | `30` | 30 minutes. |
| `H1` | `60` | 1 hour. |
| `H2` | `120` | 2 hours. |
| `H3` | `180` | 3 hours. |
| `H4` | `240` | 4 hours. |
| `H6` | `360` | 6 hours. |
| `H12` | `720` | 12 hours. |
| `D1` | `1440` | 1 day. |
| `D2` | `2880` | 2 days. |
| `D3` | `4320` | 3 days. |
| `W1` | `10080` | 1 week. |
| `MN1` | `43200` | 1 month (30 days). |

---

## 8. Gene Attributes

**Type:** `GeneAttribute` (attribute)  
**Namespace:** `Chronos.Core.Abstractions.Shared`

Used to decorate strategy properties for GA optimisation.

### Constructor Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `min` | `double` | Minimum allowed value. |
| `max` | `double` | Maximum allowed value. |
| `step` | `double` | Discretisation step. Must be `0` for `Continuous`, `Structural`, and `Parametric` gene types. For `Discrete` and `Categorical` types, `step` must be **positive** (can be any double > 0). The constructor enforces these rules. |
| `type` | `GeneType` | `Continuous`, `Discrete`, `Categorical`, `Structural`, `Parametric`. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Friendly display name (optional). |
| `Order` | `int` | Deterministic ordering in chromosome (lower = earlier). |

### GeneType Values

- `Continuous` – range with no steps.
- `Discrete` – stepped values (step must be positive).
- `Categorical` – whole‑number choices (step must be positive).
- `Structural` – topology genes (step must be 0).
- `Parametric` – neural network weights (step must be 0).

**Note:** The `step` parameter is only applicable to `Discrete` and `Categorical`. For all other types it must be `0`. The constructor will throw `ArgumentException` if this rule is violated.

---

## 9. Data Action Policy

**Type:** `DataActionPolicy` (enum)  
**Namespace:** `Chronos.Core.Abstractions.Shared`

| Value | Meaning |
|-------|---------|
| `KeepUntilExit` | File stays until engine process ends. |
| `DeleteAfterTask` | Deleted immediately after backtest/optimisation completes. |
| `PersistentCache` | Kept for future reuse. Adapter manages cleanup. |

---

## 10. Adapter Capability Interface

**Type:** `IAdapterCapability` (interface)  
**Namespace:** `Chronos.Core.Abstractions.Slots`

Replaces the previous `IAdapter`, `IHistoricalDataProvider`, `ILiveDataProvider`, and `IExecutionProvider` interfaces. An adapter declares which sub‑capabilities it supports via boolean flags.

### Capability Flags

| Flag | Type | Description |
|------|------|-------------|
| `SupportsHistoricalData` | `bool` | Whether this adapter can provide historical tick data. |
| `SupportsLiveData` | `bool` | Whether this adapter can stream live tick data. |
| `SupportsExecution` | `bool` | Whether this adapter can execute orders. |

### Core Members

| Member | Type | Description |
|--------|------|-------------|
| `Name` | `string` | Human‑readable adapter name. |
| `Calculator` | `IMarketCalculator` | Exchange‑specific financial calculator. |
| `IsConnected` | `bool` | Whether the adapter is currently connected. |

### Connection

| Member | Description |
|--------|-------------|
| `Task<bool> ConnectAsync(CancellationToken)` | Establishes the underlying connection. |
| `Task DisconnectAsync()` | Gracefully disconnects. |

### Historical Data

| Member | Description |
|--------|-------------|
| `Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(...)` | Fetches history and writes it to a binary file. |
| `Task DeleteHistoryFileAsync(string)` | Deletes a previously cached binary file. |
| `Task NotifyFileSafeToDeleteAsync(string)` | Called by Chronos after it has finished reading the binary file. |

### Live Data

| Member | Description |
|--------|-------------|
| `Task SubscribeAsync(string)` | Subscribes to tick updates for the given symbol. |
| `Task UnsubscribeAsync(string)` | Unsubscribes from tick updates. |
| `event Action<string, Tick> OnTickReceived` | Raised for every received tick. |

### Execution

| Member | Description |
|--------|-------------|
| `Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest)` | Submits a new order. |
| `Task<AdapterOrderResponse> ModifyOrderAsync(long, double?, double?, double?)` | Modifies an existing order. |
| `Task<AdapterOrderResponse> ClosePositionAsync(long, double?)` | Closes a position (or partially closes it). |
| `Task<AdapterOrderResponse> CancelAsync(long)` | Cancels a pending order. |
| `Task<(double Balance, double Equity)> GetAccountInfoAsync(...)` | Returns current account balance and equity. |
| `Task<IReadOnlyList<Position>> GetActivePositionsAsync()` | Returns all currently open positions. |
| `Task<IReadOnlyList<Order>> GetPendingOrdersAsync()` | Returns all currently pending orders. |
| `Task<SymbolProperties?> GetSymbolPropertiesAsync(string, ...)` | Fetches symbol properties from the exchange. |
| `event Action<ExecutionReport> OnExecutionUpdate` | Raised when a dynamic execution update is received. |

### Symbol Support

| Member | Description |
|--------|-------------|
| `TimeFrame[]? GetSupportedTimeframes(string)` | Returns supported timeframes, or null if all are supported. |

---

## 11. Strategy Capability Interface

**Type:** `IStrategyCapability` (interface)  
**Namespace:** `Chronos.Core.Abstractions.Slots`

Replaces the previous `IStrategy` interface. Strategies are slot capabilities discovered in the `Strategies/` directory.

### Lifecycle

| Member | Description |
|--------|-------------|
| `Task OnConfigureAsync(StrategySpecification)` | Called once before any data is processed. Receives immutable configuration. |
| `Task OnStartAsync(IIndicatorRegistry)` | Called at the start of a run. The indicator registry is ready. |
| `void OnTick(string symbol, Tick tick)` | Called for every tick in chronological order. The `symbol` identifies the instrument. Must be purely synchronous to guarantee determinism. |
| `Task OnStopAsync()` | Called at the end of a run. |

### Gene Support

| Member | Description |
|--------|-------------|
| `int TotalGeneCount` | Total number of genes in the chromosome (property genes + neural network parameters). |
| `void InjectGenes(double[])` | Injects a chromosome's gene values into the strategy. |
| `double[] ExportGenes()` | Exports the current gene values from the strategy. |

### Neural Network

| Member | Description |
|--------|-------------|
| `bool RequiresNeuralNetwork` | Whether this strategy requires a neural network model. |
| `INeuralNetworkModel? NeuralNetwork` | The neural network model, set by the engine before initialization. |

---

## 12. Hook System

**Namespace:** `Chronos.Core.Abstractions.Hooks`

Chronos uses a priority‑based hook system for extensibility. Hook plugins implement `IHookManifest` and register callbacks on named hook points.

### Hook Registration Interfaces

| Interface | Description |
|-----------|-------------|
| `IHookManifest` | Entry point for hook plugins. `void RegisterHooks(IHookRegistry registry)` |
| `IHookRegistry` | Root registry with `Backtest`, `Live`, `Optimization`, `Report` sub‑registries. |
| `IFilterRegistration<T>` | Registration point for a filter hook. `void Register(Func<T, IHookContext, FilterResult<T>>, int priority)` |
| `IActionRegistration<T>` | Registration point for a typed action hook. Callbacks must be synchronous; `async void` is prohibited and will cause process crashes. |
| `IActionRegistration` | Registration point for a parameterless action hook. Same synchronous requirement. |

### Filter Results

```csharp
public static class FilterResult
{
    public static FilterResult<T> Allow<T>(T data);
    public static FilterResult<T> Reject<T>(string reason);
}

public readonly struct FilterResult<T>
{
    public bool IsAllowed { get; }
    public T? Data { get; }
    public string? RejectionReason { get; }
}
```

### Hook Contexts

| Interface | Pipeline | Key Members |
|-----------|----------|-------------|
| `IHookContext` | Base | `HookName`, `UtcNow`, `CancellationToken` |
| `IBacktestContext` | Backtesting | `CurrentTick`, `CurrentEquity`, `CurrentDrawdown`, `Broker`, `TickWindow`, `OpenPositions` |
| `ILiveContext` | Live Trading | `CurrentTick`, `CurrentEquity`, `Broker`, `AdapterName`, `IsConnected` |
| `IOptimizationContext` | Optimization | `CurrentGeneration`, `TotalGenerations`, `BestFitness`, `IsHyperMutation` |
| `IReportContext` | Reports | `ReportFormat` |

---

## 13. Validation Principles

- Every specification record has a `Validate()` method throwing `ConfigurationException`.
- No critical parameter is silently defaulted (Principle 9).
- All parsing uses `TryParse`‑style methods with clear error messages.
- Fitness evaluation is performed by hook plugins via the `optimization.*` hooks, not by a built‑in `IFitnessModel`.
- Slippage and commission are adapter‑internal concerns, handled through the adapter's `IMarketCalculator` and `SymbolProperties`, not through a user‑supplied `ISimulationFriction`.
- All seeds used for deterministic randomness must be non‑negative; negative values are rejected.
- Duplicate symbols in `RequestedSymbols` are forbidden and will cause validation failure.