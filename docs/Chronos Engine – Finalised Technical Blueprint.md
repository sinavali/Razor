# Chronos Engine – Finalised Technical Blueprint

**Version:** 1.0.0 LTS  
**Status:** Authoritative – Single Source of Truth  
**Last Updated:** 2026-07-09  

---

## 1. Overview & Core Principles

The Chronos Engine is the headless execution node that runs on the user's infrastructure. It is the **sole execution point** for all trading, backtesting, optimisation, and data‑streaming tasks. It communicates exclusively with Chronos Cloud via a persistent, encrypted WebSocket connection.

The Engine is designed to be:

- **Always‑online** – maintains connection to Cloud at all times. If the connection drops, it retries indefinitely (with exponential backoff) and never exits on its own.
- **Fully controllable** – Cloud sends granular commands to manage every aspect of the Engine's operation (60+ commands, all implemented in `Chronos.Core.Engine`).
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
     Chronos.Core.Engine.exe --auth=MyUsername,MyPassword,MyInstanceApiKey
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

All messages are JSON (except binary chunks). Envelope structure (`CloudMessage` in `Chronos.Core.Engine.Communication`):

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
   - `EngineVersion`, `ClientCapabilities` (list of supported feature IDs)
   - `PublicKey` (ECDH ephemeral public key)
2. Cloud validates credentials. If valid, returns `AuthResponse`:
   - `Status` – `Success` or `Failure`
   - `PublicKey` – Cloud's ephemeral public key
   - `Nonce` – for deriving session key
   - `SessionId` – for future reference
   - `RequiredCapabilities` – features the Engine must support
3. Engine derives shared secret from its private key and Cloud's public key.
4. Engine → Cloud: `AuthConfirm` with a signed challenge (HMAC of nonce with session key).
5. Cloud verifies and sends `AuthAck`.
6. From this point, all messages are encrypted with AES‑256‑GCM using the derived session key. Each message includes a sequence number to prevent replay.

### 3.4 Heartbeat & Time Sync

- Engine sends `Heartbeat` at intervals defined by Cloud in the previous `HeartbeatResponse`.
- Heartbeat payload includes:
  - `EngineId`
  - `LocalTimestamp` (Engine's current UTC time)
  - `Health` – CPU, memory, tasks running, live tick age, etc.
- Cloud responds with `HeartbeatResponse` containing:
  - `Status` – `OK`, `Stop`, `Pause`, `Lock`, `Exit`, `Ban`
  - `NextIntervalSeconds`
  - `ServerTime` – Cloud's current UTC time (for time sync)
  - `Commands` – list of commands to execute immediately (if any)
  - `AuthValid` – true/false (re‑validates credentials)
  - `AdminMessage` – optional broadcast message (with style hints)
  - **Self‑Update Metadata** – if a new Engine version is available, the response contains `NewVersion`, `DownloadUrl`, and `Checksum`.
- Engine updates its internal clock with `ServerTime` and adjusts drift.
- If `AuthValid` is false, Engine stops all user tasks (Live, Backtest, Optimisation) and prevents new user tasks until re‑authentication.

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

1. **Sender** sends a `BinaryTransferStart` message (`BinaryTransferManager` in `Chronos.Core.Engine.Communication`).
2. **Sender** then sends one or more `BinaryChunk` messages (base64‑encoded data).
3. **Sender** finalises with a `BinaryTransferEnd` message.
4. **Receiver** can send `BinaryTransferAck` to confirm receipt or request retransmission.
5. Checksum (SHA‑256) verification is performed on completion.

**Performance:** The same WebSocket is reused, reducing latency and overhead. For extremely large files (multi‑gigabyte tick data), the engine streams directly from disk without loading the entire file into memory.

---

## 4. Command System (ID‑Based)

Every command, feature, and capability in the system is assigned a **unique numeric ID**. This ID is used for versioning, compatibility checks, and efficient routing.

### 4.1 Command ID Registry

Commands are grouped by category, each with a range of IDs. **All 60+ commands are implemented** in `Chronos.Core.Engine.Management.Commands.Handlers`.

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

### 4.2 Command Handler Design

- Each command implemented as a class inheriting from `CommandHandlerBase`.
- `CommandDispatcher` routes incoming messages based on `CommandId` to the appropriate handler.
- Handlers have access to all core services via dependency injection.
- The dispatcher is initialised in `Program.cs` and wired to the `CloudConnector`.

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
├── state/                 (SQLite state database)
├── downloads/             (received binary files)
├── backup/                (self‑update backups)
├── update/                (staged updates)
└── behavior_logs/         (compressed behaviour log files)
```

All paths are relative to the Engine executable. No configuration files exist in these directories.

### 5.2 Discovery & Activation Flow (Double Validation)

The activation flow guarantees that only compatible, signed, and Cloud‑approved extensions are loaded. This is implemented in `ExtensionManager` and `ExtensionCatalog`.

1. **Discovery (Engine):** On startup (or `ReloadExtensions`), Engine scans all directories, loads each assembly via `PluginLoadContext`, validates SDK version (`[SdkVersion]`), and builds a manifest.
2. **Manifest Send (Engine → Cloud):** Engine sends the complete manifest to Cloud via `ExtensionManifest` event.
3. **Cloud Validation & Selection:** Cloud validates signatures, compatibility, and license permissions. It selects the active set and sends `ActivateExtensions` command.
4. **Engine Re‑validation:** Engine re‑validates that all selected extensions are present, loadable, and internally consistent.
5. **Engine → Cloud `ActiveExtensionsAck`:** Engine sends back the validated set (or an error).
6. **Cloud Finalises:** Cloud acknowledges and the Engine activates the extensions.

### 5.3 Extension Lifecycle

- **Instantiation:** `Activator.CreateInstance` (or cached compiled lambda via `_ctorCache`).
- **Initialisation:** Calls `ConnectAsync` (adapter), `OnConfigureAsync`/`OnStartAsync` (strategy), sets `NeuralNetwork` if required, and `RegisterHooks` (plugins).
- **Activation Order:** Hooks registered **before** strategy starts.
- **Deactivation:** On `StopLive` or `ReloadExtensions`, Engine disposes in reverse order.

### 5.4 Safety & Compatibility

- Double validation ensures consistency.
- `PluginValidator` checks SDK version (major must match).
- `PluginSafetyValidator` checks strong‑naming in production.
- Rollback on failure (the previous active set remains loaded until the new set is fully validated).

---

## 6. Concurrency & Task Management

### 6.1 Task Types & Resource Reservation

| Task Type | Priority | CPU Reservation | Notes |
|-----------|----------|-----------------|-------|
| LiveTask | High | **1 dedicated core** | Reserved at startup; never pre‑empted |
| BacktestTask | Medium | Shared (remaining) | Configurable by Cloud |
| OptimizationTask | Medium | Shared (remaining) | Configurable |
| Other | Low | Shared | Logs, reports, etc. |

**Live Trading** always has a reserved core (hard affinity) and higher scheduling priority. The `TaskManager` uses dedicated `Thread` with `ThreadPriority.Highest` for live tasks.

### 6.2 Task State Persistence (Minimal)

- Task states are held in memory. Cloud is the source of truth.
- For recovery, the Engine sends `StateUpdate` events. Cloud stores them.
- On restart, the Engine restores live state from SQLite (`state/engine_state.db`) via `StateManager`.

---

## 7. Schedules & Cronjobs (Virtual Timers)

### 7.1 Cronjobs

- Cloud sends `SetCronJob` with `JobId`, `CronExpression`, `Command`, `Enabled`.
- Engine stores in SQLite (`CronJobManager` in `Chronos.Core.Engine.Management.Scheduling`).
- Uses `NCrontab` library to schedule.
- When triggered, executes the command asynchronously.

### 7.2 Schedules (One‑Off)

- Cloud sends `SetSchedule` with `ScheduleId`, `ScheduledTimeUtc`, `Command`, `Repeat`.
- Engine uses an in‑memory `Timer` for immediate scheduling.

---

## 8. Offline Handling & Infinite Retry

### 8.1 Connection Monitoring

- Engine maintains persistent WebSocket.
- If connection drops, it attempts reconnection with exponential backoff (starting at 1s, doubling up to 60s, then stays at 60s).

### 8.2 No Grace Period – Infinite Retry

- The Engine **never** stops live tasks or exits due to Cloud unavailability.
- It retries indefinitely until the connection is restored.
- While offline:
  - All user commands continue running using the last known configuration.
  - Outgoing data is queued in memory and persisted to SQLite (`QueuedMessages` table).
  - The Engine does not accept new commands (they are queued by Cloud).

### 8.3 User Notification

- Cloud monitors Engine heartbeat. If offline > 5 minutes, sends alerts.
- Cloud dashboard shows "Offline" with last contact time.

---

## 9. Security & Anti‑Tampering

### 9.1 Authentication

- Three‑factor: Username + Password + Instance API Key.
- Credentials entered via CLI on each start; **never stored on disk**.
- Encrypted in memory using AES‑GCM with a key derived from PBKDF2 (`SecurityManager`).

### 9.2 Encryption (Transport)

- ECDH (P‑256) for key exchange.
- AES‑256‑GCM for symmetric encryption.
- Sequence numbers to prevent replay.
- Session keys rotated every 24 hours (via `HeartbeatResponse`).

### 9.3 Binary Protection

- Obfuscation (symbol renaming, control flow, string encryption).
- Signed with private key; Cloud verifies signature on updates.
- Anti‑debugging checks (`SecurityManager.IsDebuggerAttached()`).
- Integrity checks (`SecurityManager.VerifyIntegrity()`).

### 9.4 Extension Security

- All extensions must be strong‑named (signed) in production.
- Loaded in isolated `AssemblyLoadContext`s.
- SDK version and capability requirements validated before loading.

---

## 10. Logging & Telemetry (Performance‑Focused)

### 10.1 Logging Infrastructure

- **Library:** Serilog (structured JSON logs via `CompactJsonFormatter`).
- **Output:** Daily‑rotated files in `logs/` with size limit (10 MB) and retention (31 days).
- **Log Levels:** Trace, Debug, Information, Warning, Error, Critical. Default Information (overridable by Cloud via `SetLogLevel`).

### 10.2 Log Streaming to Cloud

- Cloud sends `GetLogs` with optional filters.
- Engine responds with a binary transfer of the matching log file(s).

### 10.3 Telemetry

- **Metrics:** OpenTelemetry (via `System.Diagnostics.Metrics`).
- Engine collects both Kernel metrics (`CoreMetrics`) and Engine‑specific metrics (`EngineTelemetry`).
- Metrics include: command execution count, task count, connection state, live tick age, CPU usage, memory usage.

---

## 11. BehaviorRecorder (Sparse, Performance‑Optimised)

### 11.1 Purpose

Record **only** the necessary data for RL training: the strategy's state at decision points, the action taken, and the resulting reward.

### 11.2 What Is Recorded (Per Record)

- `TimestampUtc` – UTC time of the decision.
- `SessionId` – unique ID for the backtest/live session.
- `State` – a dictionary of indicator values, positions, equity, etc.
- `Action` – `Buy`, `Sell`, `Close`, `Modify`, `None`.
- `Reward` – change in equity since the last recorded state.

### 11.3 Storage & Flush to Cloud

- **Local file:** Compressed binary (MessagePack + GZip) in `behavior_logs/`.
- **Flush:** Engine flushes to Cloud periodically (interval configured via Cloud).
- **After successful upload:** Local file is deleted.

### 11.4 Performance Optimisations

- **No‑op if disabled.**
- **Batching:** Records buffered in memory and flushed to disk asynchronously.
- **Compression:** GZip on the fly.
- **Low‑priority I/O.**

---

## 12. Self‑Update (Via Heartbeat)

### 12.1 Process

1. Cloud includes `NewVersion`, `DownloadUrl`, and `Checksum` in the `HeartbeatResponse`.
2. Engine detects that a new version is available.
3. Engine downloads the new binary to a temporary location.
4. Engine verifies the SHA‑256 checksum.
5. **Compatibility Check:** Engine checks that all currently loaded extensions' required capabilities are supported by the new Engine's capability set.
6. If compatible, Engine stages the new binary (moves it to `update/` directory).
7. Engine writes a pending marker (`update.pending`) with version and backup path.
8. Engine launches the new binary with `--command=restart` and exits.
9. The new binary, upon seeing `--command=restart`, finalises the replacement (replaces the original executable) and resumes normal operation.

### 12.2 Rollback

- Old executable kept as backup in `backup/`.
- If new version fails to start, automatic rollback attempted (via `RollbackAsync`).

---

## 13. State Persistence (Cloud as Source of Truth)

### 13.1 In‑Memory Only

- The Engine holds **no persistent database** for configurations. SQLite is used only for:
  - Engine ID (`Metadata` table)
  - Live state snapshots (`LiveState` table)
  - Optimisation state snapshots (`OptimizationStates` table)
  - Cron jobs (`CronJobs` table)
  - Schedules (`Schedules` table)
  - Queued outgoing messages (`QueuedMessages` table)
  - Extension manifest (`ExtensionManifest` table)

### 13.2 Cloud Synchronisation

- Engine sends `StateUpdate` events for significant changes.
- Cloud may request full state via `GetState`.

---

## 14. Error Handling & Recovery

### 14.1 Global Exception Handler

- Catch unhandled exceptions, log, attempt graceful shutdown.
- Send final status to Cloud before exit.

### 14.2 Task‑Level Error Handling

- Each task has try‑catch; on fault, task marked `Faulted`; Engine notifies Cloud.
- Live task: if faulted, Engine attempts restart (if configured) or stops.

### 14.3 Adapter Failures

- Log, try reconnect.
- If unreachable, Live task stopped.

### 14.4 Kill‑Switch

- `KillSwitch` command: closes all positions, stops all tasks, sends final status, exits.

---

## 15. Platform‑Specific Details

### 15.1 Windows

- Can run as Windows Service (`--service` flag).
- Signal handling: `Console.CancelKeyPress`, `SessionEnding`.

### 15.2 Linux (Ubuntu)

- Systemd unit file.
- Signal handling: SIGTERM, SIGINT, SIGHUP.

### 15.3 General

- All times UTC.
- Paths relative to Engine executable.
- Uses `Environment.ProcessorCount` for core count.

---

## 16. Testing Strategy

### 16.1 Unit Tests

- Test each component in isolation: command dispatcher, task manager, extension manager, state manager, cloud connector, schedule manager.

### 16.2 Integration Tests

- Test Engine as a whole: startup, authentication, full command flow, multi‑task concurrency, offline retry, extension reload.

### 16.3 Cross‑Project Integration Tests

- Engine + Kernel: run backtest via Engine, verify results; live trading with mock adapter.

### 16.4 Performance/Load Tests

- Simulate heavy optimisation while live trading; measure CPU/memory, task switching overhead.

---

## 17. ID System for Features & Capabilities

### 17.1 Feature ID Registry

| Feature | ID | Description |
|---------|----|-------------|
| Live Trading | 100 | Core live trading capability |
| Backtesting | 101 | Backtest execution |
| Optimisation | 102 | GA optimisation |
| Neural Networks | 103 | Support for `INeuralNetworkModel` |
| Hooks | 104 | Hook system support |
| Cronjobs | 105 | Scheduled jobs |
| Schedules | 106 | One‑off scheduled commands |
| Self‑Update | 108 | Automatic binary update |
| Log Streaming | 109 | On‑demand log transfer |
| Telemetry Export | 110 | Metrics export |
| Behavior Logging | 111 | Sparse behaviour recording |

### 17.2 Capability Negotiation

- Engine sends its `Capabilities` (list of supported feature IDs) during handshake.
- Cloud validates and may reject the Engine if it lacks required features.

---

## 18. Admin Broadcast Messages

- Cloud sends `BroadcastMessage` with `Text`, `Style` (info, warning, error, success).
- Engine displays on console with appropriate colour styling.

---

## 19. Performance Considerations (Summary)

| Area | Strategy |
|------|----------|
| **Tick Processing** | Avoid allocations; use `Span<T>`, `ArrayPool`; hot paths are allocation‑free. |
| **Data Recording** | Sparse (only on action), batched, compressed, asynchronous I/O. |
| **Logging** | Structured JSON; buffered writes; daily rotation. |
| **Telemetry** | Lock‑free histograms/counters; minimal overhead. |
| **WebSocket** | Reuse buffers; chunked transfers; flow control. |
| **Task Scheduling** | Resource reservation prevents contention; live‑first priority. |
| **State Persistence** | Minimal SQLite usage; Cloud is source of truth. |

---

## 20. Implementation Status

All components described in this blueprint are **fully implemented** in the `Chronos.Core.Engine` project, including:

- CLI with `--auth`, `--service`, `--development`, `--command=restart`
- `CloudConnector` with infinite retry, heartbeat, binary transfers
- `SecurityManager` with ECDH, AES‑256‑GCM, integrity checks
- `CommandDispatcher` with 60+ command handlers
- `ExtensionManager` with discovery, isolation, double validation
- `TaskManager` with live‑first scheduling, state persistence
- `CronJobManager` with NCrontab and SQLite persistence
- `BehaviorRecorder` with MessagePack, GZip, and cloud upload
- `SelfUpdateManager` with download, checksum, staging, and rollback
- `KernelService` bridging to `Chronos.Core.Kernel` for backtest, live, and optimisation execution

---

## 21. Conclusion

This blueprint defines the complete, performance‑conscious, **finalised** architecture of the Chronos Engine. It incorporates all required features while maintaining security, determinism, live‑first principles, and **infinite resiliency**. All components are implemented and operational in the current codebase.

---

*This blueprint is final and approved for implementation. Any changes require architecture board review.*