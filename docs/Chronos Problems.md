Your codebase is solid, but I've identified several critical architectural violations, severe performance bottlenecks, and dangerous logic flaws that will break determinism or cause production failures.

#### Critical Architecture & Logic Bugs

1. **The Determinism Killer (Guid Usage)**
* **Location:** `BacktestRunner.cs` and `LiveBroker.cs`
* **Issue:** You are using `Guid.NewGuid()` to generate `CorrelationId` and `EventId` for `OrderExecutedEvent` and `BacktestCompletedEvent`. `Guid.NewGuid()` relies on the OS's cryptographic RNG. If you run the Golden Test twice, the generated hashes of the trade histories/events will **never** match.
* **Fix:** Use an `Interlocked.Increment` static counter for IDs, or derive them deterministically from the strategy's `GeneInitializationSeed` combined with a tick counter.


2. **Time Domain Mix-Up (Metric Calculation)**
* **Location:** `LiveBroker.cs` (`OnTickAsync`)
* **Issue:** `long latencyTicks = _wallClock.GetTimestamp() - now;`
`_wallClock.GetTimestamp()` returns `Environment.TickCount64` (milliseconds since machine boot). `now` is `_marketClock.GetTimestamp()` (100-ns ticks since the year 0001). Subtracting these two entirely different time domains yields meaningless, massive numbers that will corrupt your OpenTelemetry histograms.
* **Fix:** Update `SystemClock.cs` to return `DateTime.UtcNow.Ticks` for latency comparisons, or keep `TickCount64` exclusively for monotonic timeouts.


3. **Destructive API Key Rotation**
* **Location:** `NobitexAdapter.cs` (`DeleteAllApiKeysAsync`)
* **Issue:** If the adapter hits an HTTP 401 or a key limit, it iterates through and deletes *every single API key* on the user's Nobitex account. This is incredibly dangerous for an institutional tool; it will break all other software the client is running.
* **Fix:** Only delete keys prefixed with your `_config.ApiKeyName`.


4. **MT5 Tick Timestamp Precision**
* **Location:** `Mt5BridgeClient.cs` (`HandleCentrifugoMessage` / `DispatchEvent`)
* **Issue:** The EA sends `tick.time` (which is seconds since 1970). `TickDto` maps it directly into the `Time` property of the `Tick` struct. Chronos expects `Tick.Time` to be in **.NET UTC Ticks** (100-ns intervals since 0001-01-01). All your timeframes and SMT divergence timing logic will completely break because the numbers are orders of magnitude off.
* **Fix:** Convert Unix milliseconds to .NET Ticks at the bridge level using your `TimeHelpers.FromUnixMilliseconds`.


5. **SimulatedBroker Holding Cost Discrepancy**
* **Location:** `SimulatedBroker.cs` (`ProcessHoldingCosts`)
* **Issue:** The simulated broker iterates over `_positions` to calculate holding costs, but it applies the cost cumulatively to `p.Swap` *without adjusting the actual equity or balance*. Furthermore, the swap is calculated from `0` to `currentTime` on *every single day boundary*, meaning the cost grows exponentially instead of linearly for multi-day holds.
* **Fix:** Pass `_nextHoldingCostTime - TimeSpan.TicksPerDay` as the `fromTime` and `currentTime` as the `toTime`. Apply the delta directly to `floatPl`.


6. **Inconsistent SDK Versioning**
* **Location:** `Directory.Build.props`, `global.json`, and Documentation.
* **Issue:** Your documentation clearly states Chronos targets .NET 9 LTS. However, your project files are locked to .NET 10.0. If you compile this, plugins targeting `net9.0` will load, but the host environment is technically .NET 10.



#### Severe Performance Bottlenecks

7. **$O(N)$ TickWindow Lookback**
* **Location:** `TickWindow.cs` (`GetCurrentStats`)
* **Issue:** `while (first >= 0 && buffer[first].Time >= windowStart) first--;`
If a timeframe is `H1` and the symbol has high tick density (e.g., 50,000 ticks per hour), this `while` loop iterates backward 50,000 times *for every single incoming tick*. This turns an $O(1)$ streaming engine into an $O(N)$ CPU nightmare.
* **Fix:** `TickRingBuffer` must maintain internal, rolling OHLC state per requested timeframe, rather than iterating backward to calculate it on demand.


8. **Memory-Mapped File 2GB Limit**
* **Location:** `MemoryMappedTickList.cs`
* **Issue:** `_count = (int)(dataLength / tickSize);`
An `int` limits you to ~2.14 billion ticks. While high, institutional backtests spanning 10 years of multi-asset level-1 data will exceed this.
* **Fix:** Implement chunked array access or map the view in segments, updating `_count` to a `long`, or clearly document the 2GB per-file limit.



#### Implementation Flaws & Gaps

9. **No Indicator Cleanup**
* **Location:** `StrategyBase.cs`
* **Issue:** Strategies generate indicators on the fly via `Indicators.Get<T>()`. However, if a strategy dynamically changes periods during a live run (e.g., adaptive moving averages), the `IndicatorRegistry` will accumulate memory forever. There is no `Remove` or `Unregister` method in `IIndicatorRegistry`.


10. **Nobitex WebSocket Deserialization**
* **Location:** `NobitexAdapter.cs` (`HandleCentrifugoMessage`)
* **Issue:** `double.Parse(ordersData["filledAmount"]!.ToString(), ...)`
If the exchange sends a partial update without the `filledAmount` field, `.ToString()` on a null node throws a `NullReferenceException`. Though caught by your generic catch block, it drops the execution report entirely, causing desynchronization (violating Principle 13).


11. **Parallel Genetic Evaluation Contention**
* **Location:** `GeneticOptimizer.cs` (`EvaluateAsync`)
* **Issue:** You pass `CancellationToken` correctly, but the actual GA `Sort()` occurs immediately after `Parallel.ForEachAsync`. If any `evaluator` tasks hang or fault silently without throwing, `c.Fitness` remains `double.MinValue`, ruining the elitism selection.


12. **MT5 Bridge EA Concurrency**
* **Location:** `Mql5BridgeEA.mq5` (`OnTimer`)
* **Issue:** The EA only processes network buffers in the `OnTimer` loop (10ms). If `recv` reads exactly `msgLen`, it parses the JSON. If the C# adapter sends two commands within 1 millisecond (e.g., `executeOrder` followed instantly by `modifyOrder`), TCP nagling combines them. `recv(ClientSocket, buffer, 4, 0)` will read the first header, but the subsequent read might pull bytes from the second message, misaligning the entire socket stream.
* **Fix:** Implement a robust state-machine buffer parser in MQL5 that safely splits concatenated TCP packets using the 4-byte length prefixes.
