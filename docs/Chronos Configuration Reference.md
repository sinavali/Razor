## Chronos Configuration Reference

**Version:** 1.0.0 LTS
**Audience:** Plugin developers & power users
**Status:** Authoritative
**Last Updated:** 2026-06-02

---

### Purpose

This document catalogues every configuration object, enumeration, and data contract that controls Chronos engine behaviour. It is the single source of truth for parameter names, types, validation rules, and acceptable values. Use it when writing strategies, building adapters, or interpreting Cloud‑supplied payloads.

---

## 1. Strategy Specification

**Type:** `StrategySpecification` (immutable record)
**Namespace:** `Chronos.Core.Abstractions.Strategies`

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `InitialBalance` | `double` | Yes | Starting account balance in quote currency. |
| `Leverage` | `double` | Yes | Account‑wide leverage multiplier (e.g., 10, 50). Must be `> 0`. |
| `FrictionModel` | `ISimulationFriction?` | No | Custom slippage/commission model for backtesting. If `null`, adapter default is used. |
| `FitnessModel` | `IFitnessModel?` | No | Scoring function for optimisation runs. Required for any GA operation. |
| `RequestedSymbols` | `ImmutableArray<SymbolRequest>` | Yes | List of symbols and their timeframes the strategy needs. At least one element required. |

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
| `HistoricalDataPolicy` | `DataActionPolicy` | No | `DeleteAfterTask` | Defines how adapter should handle binary tick files after task completion. |
| `GeneInitializationSeed` | `int` | Yes | — | Master seed for deterministic gene initialization when no explicit genes are provided. |

### Enum: `DataActionPolicy`

| Value | Meaning |
|-------|---------|
| `KeepUntilExit` | Keep the file until the engine process terminates. |
| `DeleteAfterTask` | Delete the file immediately after the backtest/optimisation finishes. |
| `PersistentCache` | Keep the file for potential reuse (adapter manages cleanup). |

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
    "GeneInitializationSeed": 12345,
    "HistoricalDataPolicy": "DeleteAfterTask"
}
```

---

## 3. Optimization Specification

**Type:** `OptimizationSpecification` (immutable record)
**Namespace:** `Chronos.Core.Kernel.Configuration`

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MasterSeed` | `int` | Yes | — | Master seed for the entire GA run. Guarantees reproducibility. |
| `Generations` | `int` | Yes | — | Number of generations to evolve. Must be `> 0`. |
| `PopulationSize` | `int` | Yes | — | Individuals per generation. Must be `≥ 4`. |
| `Windows` | `int` | Yes | — | Number of walk‑forward windows. `1` = simple optimisation. |
| `TrainSplit` | `double` | Yes | — | Fraction of data used for training (0‑1 exclusive). |
| `MutationRate` | `double` | Yes | — | Base probability of gene mutation. `[0, 1]`. |
| `CrossoverRate` | `double` | Yes | — | Probability that a gene comes from the first parent. `[0, 1]`. |
| `ElitismPct` | `double` | Yes | — | Fraction of best individuals preserved unchanged. `[0, 1]`. |
| `TournamentSize` | `int` | Yes | — | Number of individuals competing in selection. Must be `≥ 2`. |
| `StagnationGenerationsBeforeHyper` | `int` | No | `3` | Generations without improvement before hyper‑mutation activates. `≥ 1`. |
| `WalkForwardMode` | `WalkForwardMode` | No | `Anchored` | Walk‑forward window strategy. |
| `FitnessModel` | `IFitnessModel?` | Yes | — | Fitness function (must not be `null`). |

### Enum: `WalkForwardMode`

| Value | Meaning |
|-------|---------|
| `Anchored` | Training window start fixed at beginning of data; test window slides forward. |
| `Rolling` | Both training and test windows slide forward together. |

### Validation

- `Generations > 0`, `PopulationSize ≥ 4`, `Windows > 0`.
- `0 < TrainSplit < 1`.
- `0 ≤ MutationRate ≤ 1`, `0 ≤ CrossoverRate ≤ 1`, `0 ≤ ElitismPct ≤ 1`.
- `TournamentSize ≥ 2`.
- `StagnationGenerationsBeforeHyper ≥ 1`.
- `FitnessModel` must not be `null`.

### Example

```json
{
    "MasterSeed": 42,
    "Generations": 50,
    "PopulationSize": 100,
    "Windows": 3,
    "TrainSplit": 0.7,
    "MutationRate": 0.1,
    "CrossoverRate": 0.5,
    "ElitismPct": 0.05,
    "TournamentSize": 3,
    "StagnationGenerationsBeforeHyper": 3,
    "WalkForwardMode": "Anchored"
}
```

---

## 4. Live Specification

**Type:** `LiveSpecification` (immutable record)
**Namespace:** `Chronos.Core.Kernel.Configuration`

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MagicNumber` | `int` | Yes | — | Unique number to tag orders from this strategy instance. Must be `> 0`. |
| `OrderGuardTimeoutSeconds` | `int` | No | `5` | Window (in seconds) during which duplicate orders are rejected. Must be `> 0`. |
| `ContinuousOptimization` | `bool` | No | `false` | If `true`, the Cloud will schedule periodic optimisations. |
| `LookbackDays` | `int` | Required if `ContinuousOptimization` is true | — | Days of historical data to use for optimisation. Must be `> 0`. |
| `SkipRecentDays` | `int` | No | `0` | Excludes the most recent N days from optimisation data to avoid bias. |
| `NotificationChannels` | `ImmutableArray<INotificationChannel>` | No | `[]` | List of user‑defined notification handlers (email, Telegram, etc.). |
| `RotateOptimizationSeed` | `bool` | No | `false` | If `true`, the master seed is rotated for each optimisation cycle. |
| `InitialDelayMinutes` | `int` | No | `1` | Minutes to wait before the first optimisation run. |
| `OptimizationIntervalHours` | `int` | No | `24` | Hours between automated optimisations. Must be `> 0`. |

### Validation

- `MagicNumber > 0`.
- `OrderGuardTimeoutSeconds > 0`.
- If `ContinuousOptimization` is `true`, `LookbackDays > 0`.
- `SkipRecentDays ≥ 0`.
- `InitialDelayMinutes ≥ 0`.
- `OptimizationIntervalHours > 0`.

### Example

```json
{
    "MagicNumber": 123456,
    "OrderGuardTimeoutSeconds": 10,
    "ContinuousOptimization": true,
    "LookbackDays": 90,
    "SkipRecentDays": 3,
    "RotateOptimizationSeed": false,
    "InitialDelayMinutes": 5,
    "OptimizationIntervalHours": 24
}
```

---

## 5. Neural Network Specification

**Type:** `NeuralNetworkSpecification` (immutable record)
**Namespace:** `Chronos.Core.Abstractions.Strategies`

### Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Topology` | `int[]` | Yes | — | Neuron counts per layer (input..output). At least two layers required. All sizes `> 0`. |
| `Activation` | `ActivationFunction` | No | `Tanh` | Hidden layer activation function. |
| `ModelType` | `string` | No | `"FeedForward"` | Type of network. Currently `"FeedForward"` or `"LSTM"` (future). |

### Enum: `ActivationFunction`

| Value | Equation |
|-------|----------|
| `Sigmoid` | `1 / (1 + e⁻ˣ)` |
| `Tanh` | `tanh(x)` |
| `ReLU` | `max(0, x)` |
| `LeakyReLU` | `max(0.01x, x)` |
| `Linear` | `x` |

### Validation

- `Topology` must not be `null`, must have at least 2 elements, and all values `> 0`.
- `ModelType` must be one of `"FeedForward"` or `"LSTM"` (case‑insensitive).

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
**Namespace:** `Chronos.Core.Abstractions.Strategies`

Used to decorate strategy properties for GA optimisation.

### Constructor Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `min` | `double` | Minimum allowed value. |
| `max` | `double` | Maximum allowed value. |
| `step` | `double` | Discretisation step. `0` for continuous. |
| `type` | `GeneType` | `Continuous`, `Discrete`, `Categorical`, `Structural`, `Parametric`. |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Friendly display name (optional). |
| `Order` | `int` | Deterministic ordering in chromosome (lower = earlier). |

### GeneType Values

- `Continuous` – range with no steps.
- `Discrete` – stepped values.
- `Categorical` – whole‑number choices.
- `Structural` – topology genes.
- `Parametric` – neural network weights.

---

## 9. Data Action Policy

**Type:** `DataActionPolicy` (enum)
**Namespace:** `Chronos.Core.Abstractions.Adapters`

| Value | Meaning |
|-------|---------|
| `KeepUntilExit` | File stays until engine process ends. |
| `DeleteAfterTask` | Deleted immediately after backtest/optimisation completes. |
| `PersistentCache` | Kept for future reuse. Adapter manages cleanup. |

---

## 10. Friction & Fitness Interfaces

### `ISimulationFriction`

```csharp
double CalculateSlippage(string symbol, OrderType type, double volume, double currentPrice);
double CalculateCommission(string symbol, double volume);
```

### `IFitnessModel`

```csharp
double Evaluate(double finalBalance, double initialBalance, double maxDrawdown,
    double maxDailyDrawdown, int totalTrades, IReadOnlyList<Position> history);
```

Higher return value = better fitness.

---

## 11. Validation Principles

- Every specification record has a `Validate()` method throwing `ConfigurationException`.
- No critical parameter is silently defaulted (Principle 9).
- All parsing uses `TryParse`‑style methods with clear error messages.