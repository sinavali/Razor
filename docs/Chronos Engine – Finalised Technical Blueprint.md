# Chronos Engine – Finalised Technical Blueprint (v1.0.0 LTS)

**Version:** 1.0.0 LTS  
**Status:** Authoritative – Single Source of Truth  
**Last Updated:** 2026-06-20  

---

## 1. Overview & Core Principles

The Chronos Engine is the headless execution node that runs on the user’s infrastructure. It is the **sole execution point** for all trading, backtesting, optimisation, and data‑streaming tasks. It communicates exclusively with Chronos Cloud via a persistent, encrypted WebSocket connection.

The Engine is designed to be:

- **Always‑online** – maintains connection to Cloud at all times. If the connection drops, it retries indefinitely (with exponential backoff) and never exits on its own.
- **Fully controllable** – Cloud sends granular commands to manage every aspect of the Engine’s operation (50+ commands).
- **Secure** – three‑factor authentication (username, password, instance API key), end‑to‑end encryption, anti‑tampering.
- **Performant** – resource‑aware task scheduling with strict live‑first prioritisation.
- **Headless & Simple** – user interacts only via Cloud dashboard; no configuration files; minimal CLI solely for authentication and critical alerts.
- **Stateless & Dumb** – all configuration, scheduling, reporting, and decision‑making live in Cloud. The Engine executes, streams raw data, and forgets.

**Core Principles (re‑stated for clarity):**

1. **Cloud‑First** – the Engine is a thin client; all logic, configuration, scheduling, and reporting are driven by Cloud. The Engine holds no persistent state beyond what is necessary for the current session.
2. **Security & Anti‑Tampering** – binary obfuscation, signing, integrity checks; encrypted communications; credentials never stored on disk.
3. **Determinism** – all backtest and optimisation tasks are deterministic; seeds are managed consistently by the Kernel.
4. **Live‑First** – live trading always has priority; resources are reserved to guarantee uninterrupted operation.
5. **Infinite Resiliency** – if Cloud connectivity is lost, the Engine retries forever and stays alive. It does not stop live tasks or shut down; it simply queues outgoing data and waits for reconnection.
6. **Extensibility** – all extension types loaded via isolated `AssemblyLoadContext`s; can be reloaded without restarting.
7. **Observability** – full logging and telemetry exposed in standard formats for integration with external monitoring tools.
8. **Performance** – every feature is designed with minimal overhead; data recording is sparse and batched; hot paths are allocation‑free where possible.

---

## 2. CLI & Startup (No Bootstrap File)

The Engine has a minimal CLI. It is launched without command‑line arguments in normal operation.

**Authentication Flow:**

1. Engine starts, displays a banner with version and capabilities.
2. **Credentials are never stored on disk.**  
   - On every fresh start, the Engine **prompts** the user interactively for:
     - Cloud Username
     - Cloud Password
     - Instance API Key
   - To support automated restarts (e.g., self‑update), the user can pass credentials via the command line:
     ```
     Chronos.Engine.exe --auth=MyUsername,MyPassword,MyInstanceApiKey
     ```
     - The `--auth` flag accepts exactly three comma‑separated values.
     - If provided, the interactive prompt is skipped.
3. Credentials are held **only in memory** and are **never** written to disk.
4. Establishes WebSocket connection to the primary Cloud endpoint (hardcoded: `wss://cloud.chronos.io/engine`).
5. Performs authentication handshake (see §4.3).
6. On success, begins normal operation (heartbeat, command listening, etc.).
7. On failure, attempts fallback endpoint (`wss://cloud.chronos-fallback.io/engine`). If all fail, it sleeps and retries indefinitely; it does not exit.

**No Persistent Files** – the Engine has no configuration file, no `.env`, no `bootstrap.json`. All operational parameters (strategy specs, execution specs, symbols, etc.) are pushed from the Cloud per command. The only local files are standard rotating logs (in `logs/`) and temporary binary tick files (managed by adapters).

**Self‑Update Restart Flag:**  
When the self‑update mechanism stages a new binary, it restarts the Engine with the `--auth` flag (carrying the stored credentials) and the `--command=restart` flag so the Engine knows it was invoked by the updater and can finalise the replacement.

---

## 3. Communication Protocol

### 3.1 WebSocket Transport

- **Protocol:** Secure WebSocket (WSS) over TLS.
- **Endpoints:** Hardcoded in `AppConstants.cs`:
  - `PrimaryEndpoint = "wss://cloud.chronos.io/engine"`
  - `FallbackEndpoint = "wss://cloud.chronos-fallback.io/engine"`
- **Fallback Logic:** Engine always attempts primary first. If primary fails, switches to fallback. Once connected to fallback, periodically checks primary and switches back when available.

### 3.2 Message Envelope

All messages are JSON (except binary chunks). Envelope structure:

```json
{
  "MessageId": "uuid",
  "MessageType": "Command | Event | Heartbeat | Response | BinaryChunk | Auth",
  "Version": "1.0",
  "Encrypted": true,
  "Payload": { ... } | "base64..."
}
```

- `MessageId` – UUID for tracking and deduplication.
- `MessageType` – discriminator for handling.
- `Encrypted` – indicates if payload is encrypted (always true after handshake).
- `Payload` – either a JSON object or base64‑encoded binary data.

### 3.3 Authentication Handshake

1. Engine → Cloud: `Auth` with:
   - `Username`, `Password`, `InstanceApiKey`
   - `EngineVersion`, `ClientCapabilities` (list of supported feature IDs, see §18)
   - `PublicKey` (ECDH ephemeral public key)
2. Cloud validates credentials. If valid, returns `AuthResponse`:
   - `Status` – `Success` or `Failure`
   - `PublicKey` – Cloud’s ephemeral public key
   - `Nonce` – for deriving session key
   - `SessionId` – for future reference
   - `RequiredCapabilities` – features the Engine must support
3. Engine derives shared secret from its private key and Cloud’s public key.
4. Engine → Cloud: `AuthConfirm` with a signed challenge (HMAC of nonce with session key).
5. Cloud verifies and sends `AuthAck`.
6. From this point, all messages are encrypted with AES‑256‑GCM using the derived session key. Each message includes a sequence number to prevent replay.

### 3.4 Heartbeat & Time Sync

- Engine sends `Heartbeat` at intervals defined by Cloud in the previous `HeartbeatResponse`.
- Heartbeat payload includes:
  - `EngineId`
  - `LocalTimestamp` (Engine’s current UTC time)
  - `Health` – CPU, memory, tasks running, live tick age, etc.
- Cloud responds with `HeartbeatResponse` containing:
  - `Status` – `OK`, `Stop`, `Pause`, `Lock`, `Exit`, `Ban`
  - `NextIntervalSeconds`
  - `ServerTime` – Cloud’s current UTC time (for time sync)
  - `Commands` – list of commands to execute immediately (if any)
  - `AuthValid` – true/false (re‑validates credentials)
  - `AdminMessage` – optional broadcast message (with style hints)
  - **Self‑Update Metadata** – if a new Engine version is available, the response contains `NewVersion`, `DownloadUrl`, and `Checksum`.
- Engine updates its internal clock with `ServerTime` and adjusts drift.
- If `AuthValid` is false, Engine stops all user tasks (Live, Backtest, Optimisation) – but **mining continues**. It prevents new user tasks until re‑authentication.

### 3.5 Command Execution

- Cloud sends a `Command` message with:
  - `CommandId` – numeric ID (from registry).
  - `CommandType` – string name (for readability).
  - `Parameters` – JSON object.
  - `TimeoutSeconds` – optional; if elapsed, Engine may cancel.
  - `CorrelationId` – to match responses.
- Engine executes the command and sends `CommandProgress` events (optional) and a final `CommandCompleted` event with result or error.
- Commands are executed asynchronously; concurrency is managed by the Task Manager.

### 3.6 Binary Transfers (Chunked over WebSocket)

All large data transfers (logs, optimisation results, raw tick data, behaviour logs, extension DLLs) use a chunked binary transfer over the **same WebSocket** to avoid opening extra ports or managing HTTP sessions.

**Protocol:**

1. **Sender** sends a `BinaryTransferStart` message containing:
   - `TransferId` – unique UUID.
   - `FileName` – name for the target file.
   - `TotalSize` – total bytes.
   - `ContentType` – e.g., `"application/octet-stream"`, `"application/json"`, `"text/plain"`.
   - `Checksum` – SHA‑256 of the whole file (for integrity).
2. **Sender** then sends one or more `BinaryChunk` messages:
   - Each contains a base64‑encoded `Data` segment (or raw binary over the WebSocket frame, but JSON‑base64 is simpler).
   - Each chunk includes `TransferId`, `Offset`, and `Data`.
   - Chunk size is configurable (default 64 KB) with a sliding window for flow control.
3. **Sender** finalises with a `BinaryTransferEnd` message.
4. **Receiver** can send `BinaryTransferAck` to confirm receipt or request retransmission of a specific chunk (`RequestRetransmit`).

**Performance:** The same WebSocket is reused, reducing latency and overhead. For extremely large files (multi‑gigabyte tick data), the engine streams directly from disk without loading the entire file into memory.

---

## 4. Command System (ID‑Based)

Every command, feature, and capability in the system is assigned a **unique numeric ID**. This ID is used for versioning, compatibility checks, and efficient routing.

### 4.1 Command ID Registry

Commands are grouped by category, each with a range of IDs.

| Category | ID Range | Description |
|----------|----------|-------------|
| System Management | 1000‑1099 | Auth, heartbeat, shutdown, restart |
| Live Trading | 1100‑1199 | StartLive, StopLive, InjectGenes, PauseLive, ResumeLive, GetLiveState, SyncLive |
| Backtesting | 1200‑1299 | RunBacktest, CancelBacktest, GetBacktestResult |
| Optimisation | 1300‑1399 | StartOptimization, CancelOptimization, PauseOptimization, ResumeOptimization, GetOptimizationState, GetOptimizationResult |
| Extensions | 1400‑1499 | ReloadExtensions, DeployExtension, RemoveExtension, ListExtensions, ActivateExtensions |
| Reports (Client‑Side Aggregation) | 1500‑1599 | *Reserved for Cloud* – Engine never generates reports |
| Logs & Telemetry | 1600‑1699 | GetLogs, DeleteLogsAll, DeleteLogsExpired, SetLogLevel, GetMetrics, ExportMetrics |
| Schedules & Cron | 1700‑1799 | SetCronJob, DeleteCronJob, ListCronJobs, SetSchedule, DeleteSchedule, ListSchedules |
| Mining | 1800‑1899 | StartMining, StopMining, GetMiningStatus, UpdateMiningConfig |
| Admin & Broadcast | 1900‑1999 | BroadcastMessage, SetAdminConfig, GetEngineCapabilities, GetEngineVersion |
| Kill & Emergency | 2000‑2099 | KillSwitch, EmergencyStop |
| Behaviour Logging | 2100‑2199 | EnableBehaviorLogging, DisableBehaviorLogging, GetBehaviorLogs, DeleteBehaviorLogs |

### 4.2 Full Command List (60+ Commands)

*(This list remains identical to the previous blueprint, but all handlers are fully implemented. See `ICommandHandler` implementations for detailed behaviour.)*

#### System Management (1000‑1099)
- `Auth` (1000) – authenticate.
- `AuthConfirm` (1001) – confirm session.
- `Heartbeat` (1002) – send heartbeat.
- `GetStatus` (1003) – return full Engine status.
- `PauseEngine` (1004) – pause non‑critical tasks.
- `ResumeEngine` (1005) – resume paused tasks.
- `Shutdown` (1006) – graceful shutdown.
- `Restart` (1007) – restart Engine (used by self‑update).
- `SetConfig` (1008) – update runtime config (from Cloud).
- `GetConfig` (1009) – return current config.
- `GetCapabilities` (1010) – return supported feature IDs.

#### Live Trading (1100‑1199)
- `StartLive` (1100) – start live trading.
- `StopLive` (1101) – stop live trading.
- `InjectGenes` (1102) – inject genes into live strategy.
- `PauseLive` (1103) – pause live (no new orders).
- `ResumeLive` (1104) – resume live.
- `GetLiveState` (1105) – get current live state.
- `SyncLive` (1106) – force reconciliation with broker.
- `SetLiveConfig` (1107) – update live config (e.g., stop‑out, max positions).
- `GetLiveMetrics` (1108) – get live performance metrics.

#### Backtesting (1200‑1299)
- `RunBacktest` (1200) – run a backtest.
- `CancelBacktest` (1201) – cancel a running backtest.
- `GetBacktestResult` (1202) – get result (raw data stream, not a report).
- `ListBacktests` (1203) – list all backtest tasks.

#### Optimisation (1300‑1399)
- `StartOptimization` (1300) – start GA optimisation.
- `CancelOptimization` (1301) – cancel.
- `PauseOptimization` (1302) – pause and save state.
- `ResumeOptimization` (1303) – resume from saved state.
- `GetOptimizationState` (1304) – get current population snapshot.
- `GetOptimizationResult` (1305) – get best chromosome (raw data).
- `ListOptimizations` (1306) – list optimisation tasks.

#### Extensions (1400‑1499)
- `ReloadExtensions` (1400) – reload all extensions.
- `DeployExtension` (1401) – deploy new DLL (binary transfer).
- `RemoveExtension` (1402) – remove extension by name.
- `ListExtensions` (1403) – return manifest.
- `ActivateExtensions` (1404) – activate specific set (re‑validate).

#### Logs & Telemetry (1600‑1699)
- `GetLogs` (1600) – request logs (type, date range). Returns binary transfer.
- `DeleteLogsAll` (1601) – delete all logs.
- `DeleteLogsExpired` (1602) – delete expired logs.
- `SetLogLevel` (1603) – change log verbosity.
- `GetMetrics` (1604) – fetch telemetry metrics (JSON).
- `ExportMetrics` (1605) – export metrics to binary file.

#### Schedules & Cron (1700‑1799)
- `SetCronJob` (1700) – define a cron job (command + schedule).
- `DeleteCronJob` (1701) – delete a cron job.
- `ListCronJobs` (1702) – list all cron jobs.
- `SetSchedule` (1703) – define a one‑off schedule (datetime + command).
- `DeleteSchedule` (1704) – delete a schedule.
- `ListSchedules` (1705) – list all schedules.

#### Mining (1800‑1899)
- `StartMining` (1800) – start mining (with config fetched from Cloud).
- `StopMining` (1801) – stop mining.
- `GetMiningStatus` (1802) – get mining status (admin only).
- `UpdateMiningConfig` (1803) – update mining config (pushed from Cloud).

#### Admin & Broadcast (1900‑1999)
- `BroadcastMessage` (1900) – display admin message on console.
- `SetAdminConfig` (1901) – set admin‑only config.
- `GetEngineCapabilities` (1902) – return supported feature IDs.
- `GetEngineVersion` (1903) – return version.

#### Kill & Emergency (2000‑2099)
- `KillSwitch` (2000) – emergency shutdown (close positions, exit).
- `EmergencyStop` (2001) – stop all tasks (but keep Engine running).

#### Behaviour Logging (2100‑2199)
- `EnableBehaviorLogging` (2100) – enable sparse behaviour logging for strategy.
- `DisableBehaviorLogging` (2101) – disable.
- `GetBehaviorLogs` (2102) – request behaviour log files (binary transfer).
- `DeleteBehaviorLogs` (2103) – delete behaviour logs.

### 4.3 Command Handler Design

- Each command implemented as a class implementing `ICommandHandler<T>` where `T` is the command type.
- Handlers are registered via DI using `AddTransient` or `AddScoped` in a dedicated `CommandRegistry`.
- `CommandDispatcher` routes incoming messages based on `CommandId` to the appropriate handler.
- Handlers have access to all core services (TaskManager, ExtensionManager, StateManager, CloudConnector, KernelService, etc.).

---

## 5. Extension & Slot Management

### 5.1 Directory Structure

```
EngineRoot/
├── Slots/
│   ├── Adapters/
│   ├── Strategies/
│   ├── Indicators/
│   └── NeuralNetworks/
├── Hooks/
├── logs/
└── behavior_logs/
```

- `Slots/` – subdirectories for each slot type.
- `Hooks/` – all hook plugin DLLs.
- `logs/` – standard logs (daily rotation).
- `behavior_logs/` – compressed behaviour log files (temporary).

All paths are relative to the Engine executable. No configuration files exist in these directories.

### 5.2 Discovery & Activation Flow (Double Validation)

The activation flow guarantees that only compatible, signed, and Cloud‑approved extensions are loaded.

1. **Discovery (Engine):** On startup (or `ReloadExtensions`), Engine scans all directories, loads each assembly via `PluginLoadContext`, validates SDK version (`[SdkVersion]`), and builds a manifest:
   - List of adapters (name, version, capabilities)
   - List of strategies (name, version, capabilities)
   - List of indicators (name, version)
   - List of NN models (name, version)
   - List of hook plugins (name, version)
2. **Manifest Send (Engine → Cloud):** Engine sends the complete manifest to Cloud via `ExtensionManifest` event.
3. **Cloud Validation & Selection:** Cloud validates signatures, compatibility, and license permissions. It selects the active set based on the user profile and sends `ActiveExtensions` command:
   - `AdapterName`
   - `StrategyName`
   - `IndicatorNames` (list)
   - `NNModelName` (optional)
   - `HookPluginNames` (list)
4. **Engine Re‑validation:** Engine re‑validates that all selected extensions are present, loadable, and that their required capabilities are supported by the Engine. It also verifies that the Cloud‑selected set is internally consistent (e.g., the selected adapter is compatible with the selected strategy’s symbol requirements).
5. **Engine → Cloud `ActiveExtensionsAck`:** Engine sends back the validated set (or an error if validation fails). If validation fails, the Engine does **not** activate the extensions and sends an error to Cloud.
6. **Cloud Finalises:** Cloud acknowledges and the Engine activates the extensions.

**When does activation happen?**
- On every fresh Engine startup (after successful Cloud authentication).
- On `ReloadExtensions` command (user manually updates extensions via Cloud dashboard).
- On `DeployExtension` / `RemoveExtension` (followed by a reload).

### 5.3 Extension Lifecycle

- **Instantiation:** `Activator.CreateInstance` (or DI factory).
- **Initialisation:** Calls `ConnectAsync` (adapter), `OnConfigureAsync`/`OnStartAsync` (strategy), sets `NeuralNetwork` if required, and `RegisterHooks` (plugins).
- **Activation Order:** Hooks registered **before** strategy starts.
- **Deactivation:** On `StopLive` or `ReloadExtensions`, Engine disposes in reverse order.

### 5.4 Safety & Compatibility

- Double validation ensures consistency.
- Compatibility check uses feature ID system (Engine compares its supported IDs against extension’s required IDs).
- Rollback on failure (the previous active set remains loaded until the new set is fully validated).

---

## 6. Concurrency & Task Management

### 6.1 Task Types & Resource Reservation

| Task Type | Priority | CPU Reservation | RAM Reservation | Notes |
|-----------|----------|-----------------|-----------------|-------|
| LiveTask | High | **1 dedicated core** | 1 GB | Reserved at startup; never pre‑empted |
| BacktestTask | Medium | Shared (remaining) | Up to 2 GB per task | Configurable by Cloud |
| OptimizationTask | Medium | Shared (remaining) | Up to 2 GB per task | Configurable |
| MiningTask | Low | Idle CPU only | 256 MB | Auto‑pauses if other tasks need CPU |
| ReportTask | Low | Shared | 256 MB | (Engine only streams raw data) |
| LogsTask | Low | Shared | 128 MB | |

**Live Trading** always has a reserved core (hard affinity) and higher scheduling priority. The `TaskManager` uses `ThreadPool` limits and custom `TaskScheduler` implementations to enforce this.

### 6.2 Task Scheduler

- `TaskManager` maintains a list of running tasks.
- Starts, stops, pauses, resumes tasks.
- Each task has a `CancellationTokenSource`.
- Tasks report progress via events sent to Cloud.

### 6.3 Resource Monitoring

- Engine periodically polls `Environment.ProcessorCount` and system memory.
- If available resources change (e.g., server upgraded), `LiveTask` adjusts its reservation accordingly (Cloud can trigger a `ReconfigureResources` command).
- The Task Scheduler respects `MaxParallelThreads` from execution specifications to avoid oversubscription.

### 6.4 Task State Persistence (Minimal)

- Task states are held in memory. Cloud is the source of truth.
- For recovery, the Engine sends `StateUpdate` events. Cloud stores them.
- On restart, the Engine does **not** restore state locally; it waits for Cloud to send the appropriate commands to rebuild the state.

---

## 7. Schedules & Cronjobs (Virtual Timers)

### 7.1 Cronjobs

- Cloud sends `SetCronJob` with:
  - `JobId`, `CronExpression`, `Command`, `Enabled`
- Engine stores in memory (not persisted locally).
- Uses `NCrontab` library to schedule.
- When triggered, executes the command asynchronously.
- Can `ListCronJobs`, `DeleteCronJob`, `Enable/Disable`.

### 7.2 Schedules (One‑Off)

- Cloud sends `SetSchedule` with:
  - `ScheduleId`, `ScheduledTimeUtc`, `Command`, `Repeat`
- Engine uses an in‑memory `Timer`.
- After execution, the schedule is deleted (unless recurring).
- All schedules are re‑sent by Cloud on Engine restart (since they are not persisted locally).

---

## 8. Offline Handling & Infinite Retry

### 8.1 Connection Monitoring

- Engine maintains persistent WebSocket.
- If connection drops, it attempts reconnection with exponential backoff (starting at 1s, doubling up to 60s, then stays at 60s).
- Tracks time since last successful connection.

### 8.2 No Grace Period – Infinite Retry

- The Engine **never** stops live tasks or exits due to Cloud unavailability.
- It retries indefinitely until the connection is restored.
- While offline:
  - All user commands (Live, Backtest, Optimisation) continue running using the last known configuration.
  - Outgoing data (results, logs, heartbeats) is queued in memory (with a bounded queue; if queue fills, older items are dropped with a warning).
  - Mining continues (if configured).
  - The Engine does not accept new commands (they are queued by Cloud and delivered when the connection re‑establishes).

### 8.3 User Notification

- Cloud monitors Engine heartbeat. If offline > 5 minutes, sends alerts (email, SMS, push).
- Cloud dashboard shows "Offline" with last contact time.

### 8.4 Command Queuing Offline

- Commands sent by Cloud while the Engine is offline are queued on the Cloud side and pushed when the Engine reconnects.
- The Engine rejects commands only if it is in a `Lock` or `Ban` state.

---

## 9. Security & Anti‑Tampering

### 9.1 Authentication

- Three‑factor: Username + Password + Instance API Key.
- Credentials entered via CLI on each start; **never stored on disk**.
- `--auth` flag supports automated restarts but the credentials are still only held in memory.
- Encrypted in memory using a key derived from the session, not from a local seed.

### 9.2 Encryption (Transport)

- ECDH (P‑256) for key exchange.
- AES‑256‑GCM for symmetric encryption.
- Sequence numbers to prevent replay.
- Session keys rotated every 24 hours (Cloud sends `RotateSessionKey`).

### 9.3 Binary Protection

- Obfuscation (symbol renaming, control flow, string encryption).
- Signed with private key; Cloud verifies signature on updates.
- Anti‑debugging checks (detect debugger; refuse to start).
- Integrity checks at runtime (hash of critical sections) – stop on mismatch.

### 9.4 Extension Security

- All extensions must be strong‑named (signed).
- Loaded in isolated `AssemblyLoadContext`s.
- SDK version and capability requirements validated before loading.
- Double validation (Engine + Cloud) prevents malicious or incompatible extensions from activating.

### 9.5 Log & Data Security

- Logs may contain sensitive data; stored with user‑only permissions.
- Sent to Cloud encrypted.

---

## 10. Logging & Telemetry (Performance‑Focused)

### 10.1 Logging Infrastructure

- **Library:** Serilog (structured JSON logs).
- **Output:** Daily‑rotated files in `logs/`, UTC date + deletion timestamp in filename: `chronos-{yyyy-MM-dd}-{deletionTimestamp}.log`.
- **Retention:** Predefined (7,10,14,30,90,365,1000 days). Deletion daily at 00:01 UTC.
- **Log Levels:** Trace, Debug, Information, Warning, Error, Critical. Default Information (overridable by Cloud).

### 10.2 Log Streaming to Cloud

- Cloud sends `GetLogs` with optional filters.
- Engine responds with a binary transfer of the matching log file(s).
- **Performance:** For current day, Engine sends only the latest file (incremental). If same parameters requested again, it sends only the delta (to save bandwidth).

### 10.3 Telemetry

- **Metrics:** OpenTelemetry (via `System.Diagnostics.Metrics`). Engine collects both Kernel metrics (`Metrics`) and Engine‑specific metrics (task count, CPU, memory).
- **Export:** Sent to Cloud via heartbeat. Also available via a local HTTP endpoint (if enabled by Cloud) for Prometheus scraping.

---

## 11. BehaviorRecorder (Sparse, Performance‑Optimised)

### 11.1 Purpose

Record **only** the necessary data for RL training: the strategy’s state at decision points, the action taken, and the resulting reward.

### 11.2 What Is Recorded (Per Record)

- `TimestampUtc` – UTC time of the decision.
- `SessionId` – unique ID for the backtest/live session.
- `State` – a dictionary containing:
  - Indicator values (with their version).
  - Current open positions (symbol, type, volume, open price, current PnL).
  - Balance, Equity, Margin used.
  - Bid, Ask.
- `Action` – `Buy`, `Sell`, `Close`, `Modify`, `None`.
- `Reward` – change in equity since the last recorded state.

**Recording triggers:**
- Strategy takes an action (default).
- Or at a fixed interval (e.g., every 10 seconds) if configured by Cloud.

### 11.3 Storage & Flush to Cloud

- **Local file:** Compressed binary (MessagePack + GZip) in `behavior_logs/`.
- **Flush:** Engine flushes to Cloud periodically (interval configured by Cloud via heartbeat response).
- **After successful upload:** Local file is deleted (or moved to archive for a configurable period).

### 11.4 Performance Optimisations

- **No‑op if disabled.**
- **Batching:** Records buffered in memory (e.g., 10,000 records) and flushed to disk asynchronously.
- **Compression:** GZip on the fly.
- **Low‑priority I/O.**
- **Sparse recording.**

---

## 12. Mining Integration (Real BTC Mining, Cloud‑Controlled)

### 12.1 Purpose

Utilise idle CPU resources for Bitcoin mining. This feature is a side monetisation channel; it must be stable for 10+ years with minimal maintenance.

### 12.2 Implementation

- **Library:** The Engine uses a well‑established, open‑source .NET Stratum client library (e.g., `StratumClient` or `NBXplorer` style) to communicate with mining pools.
- **Protocol:** Stratum (JSON‑RPC over TCP) for BTC mining.
- **Configuration:** All mining parameters are **fetched from Cloud**, never hardcoded:
  - The Engine periodically calls a public Cloud endpoint: `GET /api/mining/config` (or receives it via heartbeat).
  - Response contains: `PoolUrl`, `WalletAddress`, `Password`, `ThreadCount`, `SimulateOnly` (for testing).
  - The `InstanceApiKey` (or session token) is used to authenticate this request.
- **Control:** Cloud can start/stop mining via commands. The Engine also respects a `NoMining` claim/permission. If the claim is present (or absent depending on the tier), the Engine will reject `StartMining` commands.
- **Status:** Admin‑only via `GetMiningStatus`.
- **Resource Management:** Mining runs at `ThreadPriority.Lowest` and automatically yields CPU to live tasks, backtests, and optimisations.

### 12.3 Security

- Mining config encrypted in transit.
- Wallet address and pool URL are never stored locally.
- Mining logs excluded from user‑accessible logs.

---

## 13. Self‑Update (Via Heartbeat)

### 13.1 Process

1. Cloud includes `NewVersion`, `DownloadUrl`, and `Checksum` in the `HeartbeatResponse`.
2. Engine detects that a new version is available.
3. Engine downloads the new binary from `DownloadUrl` to a temporary location.
4. Engine verifies the SHA‑256 checksum.
5. **Compatibility Check:** Engine checks that all currently loaded extensions’ required capabilities are supported by the new Engine’s capability set. If any extension requires a feature not present, update rejected.
6. If compatible, Engine stages the new binary (moves it to a `update/` directory).
7. Engine stops all tasks gracefully, saves minimal state to memory (just enough to resume).
8. Engine launches the new binary with `--auth=... --command=restart` and exits.
9. The new binary, upon seeing `--command=restart`, finalises the replacement (moves the staged binary into the main executable path) and resumes normal operation.

### 13.2 Rollback

- Old executable kept as backup in `backup/`.
- If new version fails to start 3 times, automatic rollback attempted (the launcher script or the new binary triggers the rollback).

---

## 14. State Persistence (Cloud as Source of Truth)

### 14.1 In‑Memory Only

- The Engine holds **no persistent database**. No SQLite, no local JSON configs.
- All state (live positions, orders, balance, optimisation populations, schedules) is synchronised with Cloud via events.
- On restart, the Engine is a blank slate and waits for Cloud to send the initial state (via commands).

### 14.2 Cloud Synchronisation

- Engine sends `StateUpdate` events for significant changes.
- Cloud may request full state via `GetState`.
- Engine can pull state from Cloud if inconsistency detected (rare).

### 14.3 Behaviour Logs Exception

- Behaviour logs are the **only** data stored locally beyond logs.
- They are temporary and are deleted after successful upload to Cloud.

---

## 15. Error Handling & Recovery

### 15.1 Global Exception Handler

- Catch unhandled exceptions, log, attempt graceful shutdown.
- Send final status to Cloud before exit.

### 15.2 Task‑Level Error Handling

- Each task has try‑catch; on fault, task marked `Faulted`; Engine notifies Cloud.
- Live task: if faulted, Engine attempts restart (if configured) or stops.

### 15.3 Adapter Failures

- Log, try reconnect.
- If unreachable, Live task stopped.

### 15.4 Kill‑Switch

- `KillSwitch` command: closes all positions, stops all tasks, sends final status, exits.

---

## 16. Platform‑Specific Details

### 16.1 Windows

- DPAPI for memory encryption.
- Can run as Windows Service (`-service` flag).
- Signal handling: `Console.CancelKeyPress`, `SessionEnding`.

### 16.2 Linux (Ubuntu)

- `keyctl` for encryption (or file‑based with proper permissions).
- Systemd unit file.
- Signal handling: SIGTERM, SIGINT, SIGHUP.

### 16.3 General

- All times UTC.
- Paths relative to Engine executable.
- Uses `Environment.ProcessorCount` for core count.

---

## 17. Testing Strategy (Post‑Finalisation)

### 17.1 Unit Tests

- Test each component in isolation: command dispatcher, task manager, extension manager, state manager, cloud connector, schedule manager.

### 17.2 Integration Tests

- Test Engine as a whole: startup, authentication, full command flow, multi‑task concurrency, offline retry, extension reload.

### 17.3 Cross‑Project Integration Tests

- Engine + Kernel: run backtest via Engine, verify results; live trading with mock adapter.

### 17.4 Performance/Load Tests

- Simulate heavy optimisation while live trading; measure CPU/memory, task switching overhead.

*(Tests are planned but will be written **after** the entire solution is finalised and stable.)*

---

## 18. ID System for Features & Capabilities

To ensure extensibility and version compatibility, every feature, command, and capability is assigned a unique numeric ID.

### 18.1 Feature ID Registry

| Feature | ID | Description |
|---------|----|-------------|
| Live Trading | 100 | Core live trading capability |
| Backtesting | 101 | Backtest execution |
| Optimisation | 102 | GA optimisation |
| Neural Networks | 103 | Support for `INeuralNetworkModel` |
| Hooks | 104 | Hook system support |
| Cronjobs | 105 | Scheduled jobs |
| Schedules | 106 | One‑off scheduled commands |
| Mining | 107 | Background mining |
| Self‑Update | 108 | Automatic binary update |
| Log Streaming | 109 | On‑demand log transfer |
| Telemetry Export | 110 | Metrics export |
| Behavior Logging | 111 | Sparse behaviour recording |

### 18.2 Capability Negotiation

- Engine sends its `Capabilities` (list of supported feature IDs and versions) during handshake.
- Cloud validates and may reject the Engine if it lacks required features.
- Used during self‑update compatibility check.

---

## 19. Admin Broadcast Messages

- Cloud sends `BroadcastMessage` with `Text`, `Style` (info, warning, error, success), `Persistent` (boolean).
- Engine displays on console with appropriate colour styling (ANSI or Windows console colours).

---

## 20. Performance Considerations (Summary)

| Area | Strategy |
|------|----------|
| **Tick Processing** | Avoid allocations; use `Span<T>`, `ArrayPool`; hot paths are allocation‑free. |
| **Data Recording** | Sparse (only on action), batched, compressed, asynchronous I/O. |
| **Logging** | Structured JSON; buffered writes; daily rotation. |
| **Telemetry** | Lock‑free histograms/counters; minimal overhead. |
| **WebSocket** | Reuse buffers; chunked transfers; flow control. |
| **Task Scheduling** | Resource reservation prevents contention; live‑first priority. |
| **State Persistence** | No persistent local DB; Cloud is source of truth. |

---

## 21. Implementation Roadmap (No Phases – Integrated Whole)

The Engine is built as a single, cohesive project. All components are developed in parallel, with continuous integration and testing. The blueprint serves as the source of truth for every feature.

**Key Milestones (not phases):**
- Core infrastructure (CLI, Cloud connector, command dispatcher).
- Extension management and activation (double validation).
- Task manager and concurrency (live‑first resource reservation).
- Live, backtest, and optimisation tasks (fully integrated with Kernel).
- Schedules, cronjobs.
- BehaviorRecorder and mining (real Stratum implementation).
- Self‑update, logging, telemetry.
- Full security and anti‑tampering.
- Comprehensive testing (post‑finalisation).

---

## 22. Conclusion

This blueprint defines the complete, performance‑conscious, **finalised** architecture of the Chronos Engine. It incorporates all required features while maintaining security, determinism, live‑first principles, and **infinite resiliency**.

All components are specified with sufficient detail to begin implementation. The Engine will be the robust, scalable, and fully controllable execution node that powers the Chronos ecosystem.

---

*This blueprint is final and approved for implementation. Any changes require architecture board review.*