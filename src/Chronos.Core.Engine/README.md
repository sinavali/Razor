# Chronos.Core.Engine

**Version:** 1.0.0 LTS  
**Status:** Internal – closed source  
**Last Updated:** 2026-07-09  

---

## Overview

`Chronos.Core.Engine` is the headless executable that hosts the Chronos Kernel, manages extensions, and communicates with Chronos Cloud via a persistent encrypted WebSocket connection. It is the user‑facing binary that traders and quants deploy on their infrastructure.

The Engine is designed to be:

- **Always‑online** – Maintains connection to Cloud at all times with infinite retry.
- **Fully controllable** – Responds to 60+ granular commands from Cloud.
- **Secure** – Three‑factor authentication, end‑to‑end encryption, anti‑tampering.
- **Performant** – Live‑first task prioritisation with dedicated CPU reservation.
- **Headless & Simple** – No configuration files; minimal CLI solely for authentication.

---

## Key Components

### 1. Program.cs – Entry Point

The main entry point handles:

- **CLI parsing:** `--auth`, `--service`, `--development`, `--command=restart`, `--help`, `--version`
- **Credential management:** Interactive prompt or `--auth` flag; credentials encrypted in memory.
- **Host building:** `BuildServiceProvider()` registers all services via dependency injection.
- **Shutdown handling:** `Console.CancelKeyPress` and `UnhandledException` handlers.
- **Service mode:** Supports Windows Service (`--service`) and systemd.

**CLI Reference:**

| Flag | Description |
|------|-------------|
| `--auth=username,password,apikey` | Sets credentials via command line. |
| `--help`, `-h` | Shows help message. |
| `--version`, `-v` | Shows version information. |
| `--service` | Runs as a Windows Service or systemd. |
| `--development` | Runs in development mode (disables some security checks). |
| `--command=restart` | Internal use for self‑update. |

---

### 2. Communication (`Chronos.Core.Engine.Communication`)

#### CloudConnector

Manages the WebSocket connection to Chronos Cloud.

- **Connection:** Attempts primary endpoint (`wss://cloud.chronos.io/engine`) with fallback (`wss://cloud.chronos-fallback.io/engine`).
- **Authentication:** ECDH key exchange + AES‑256‑GCM encryption.
- **Heartbeat:** Periodic health reporting (CPU, memory, tasks, live tick age).
- **Infinite retry:** Exponential backoff starting at 1s, doubling to 60s.
- **Command dispatch:** Receives commands and forwards to `CommandDispatcher`.
- **Binary transfers:** Chunked file transfers over WebSocket (logs, results, extensions).

#### BinaryTransferManager

Manages state for ongoing binary transfers.

- **Incoming:** Receives chunks, verifies SHA‑256 checksum, assembles files.
- **Outgoing:** Sends chunks with flow control and retransmit support.
- **Cleanup:** Automatic deletion of temporary files on cancellation or completion.

---

### 3. Core Services (`Chronos.Core.Engine.Core`)

| Service | Description |
|---------|-------------|
| `SecurityManager` | ECDH key exchange, AES‑256‑GCM encryption, integrity checks, anti‑debugging. |
| `StateManager` | SQLite persistence for engine ID, live state, optimisation state, cron jobs, schedules, queued messages. |
| `Credentials` | In‑memory credential storage; encrypted with PBKDF2 + AES‑GCM; never persisted to disk. |
| `EngineTelemetry` | OpenTelemetry metrics for engine‑specific data (connection state, command execution, task counts). |
| `LoggingService` | Serilog initialisation with JSON format, daily rotation, size limits, and retention. |
| `ConfigStore` | In‑memory runtime configuration store. |
| `ExceptionLogger` | Centralised exception logging with correlation ID support. |

---

### 4. Extensions (`Chronos.Core.Engine.Extensions`)

| Component | Description |
|-----------|-------------|
| `ExtensionManager` | Discovers, activates, and manages extensions. Implements `IExtensionManager`. |
| `ExtensionCatalog` | Scans directories, loads assemblies via `PluginLoadContext`, validates SDK version and signing. |
| `PluginLoadContext` | Isolated `AssemblyLoadContext` for extensions; supports unloading. |
| `PluginValidator` | Validates SDK version (major must match). |
| `PluginSafetyValidator` | Validates strong‑naming in production mode. |

**Activation flow (double validation):**
1. Engine scans directories and builds manifest.
2. Manifest sent to Cloud.
3. Cloud validates and sends active set.
4. Engine re‑validates and activates.
5. Engine sends acknowledgment to Cloud.

---

### 5. Kernel Facade (`Chronos.Core.Engine.Kernel`)

`KernelService` bridges Engine commands to `Chronos.Core.Kernel` operations.

| Method | Description |
|--------|-------------|
| `StartBacktestAsync()` | Starts a backtest with the given adapter, strategy, and configuration. |
| `GetBacktestResultAsync()` | Retrieves completed backtest results. |
| `StartLiveAsync()` | Starts a live trading session with the active adapter and strategy. |
| `StopLiveAsync()` | Stops a live trading session. |
| `PauseLiveAsync()` / `ResumeLiveAsync()` | Pauses/resumes live trading (no new orders). |
| `InjectGenesAsync()` | Injects genes into the live strategy. |
| `StartOptimizationAsync()` | Starts a GA optimisation run. |
| `GetOptimizationResultAsync()` | Retrieves the best chromosome from a completed optimisation. |

---

### 6. Command Dispatcher (`Chronos.Core.Engine.Management.Commands`)

| Component | Description |
|-----------|-------------|
| `CommandDispatcher` | Routes incoming commands to registered handlers. |
| `CommandHandlerBase` | Base class for all command handlers. |
| `CommandIds` | Central registry of all 60+ command IDs. |

**All 60+ commands are fully implemented** in `Chronos.Core.Engine.Management.Commands.Handlers`.

**Command categories:**
- System Management (1000‑1099): Auth, heartbeat, shutdown, restart
- Live Trading (1100‑1199): StartLive, StopLive, InjectGenes, PauseLive, ResumeLive, GetLiveState, SyncLive
- Backtesting (1200‑1299): RunBacktest, CancelBacktest, GetBacktestResult
- Optimisation (1300‑1399): StartOptimization, CancelOptimization, PauseOptimization, ResumeOptimization, GetOptimizationState, GetOptimizationResult
- Extensions (1400‑1499): ReloadExtensions, DeployExtension, RemoveExtension, ListExtensions, ActivateExtensions
- Reports (1500‑1599): GenerateReport, GetReport
- Logs & Telemetry (1600‑1699): GetLogs, DeleteLogsAll, DeleteLogsExpired, SetLogLevel, GetMetrics, ExportMetrics
- Schedules & Cron (1700‑1799): SetCronJob, DeleteCronJob, ListCronJobs, SetSchedule, DeleteSchedule, ListSchedules
- Admin & Broadcast (1900‑1999): BroadcastMessage, SetAdminConfig, GetEngineCapabilities, GetEngineVersion
- Kill & Emergency (2000‑2099): KillSwitch, EmergencyStop
- Behaviour Logging (2100‑2199): EnableBehaviorLogging, DisableBehaviorLogging, GetBehaviorLogs, DeleteBehaviorLogs

---

### 7. Task Manager (`Chronos.Core.Engine.Management.Tasks`)

Manages all engine tasks with live‑first priority.

| Task Type | Priority | CPU Reservation |
|-----------|----------|-----------------|
| `LiveTask` | High | Dedicated thread with `ThreadPriority.Highest` |
| `BacktestTask` | Medium | ThreadPool (configurable) |
| `OptimizationTask` | Medium | ThreadPool (configurable) |

**Task states:** `Initializing`, `Running`, `Paused`, `Completed`, `Canceled`, `Faulted`.

**State persistence:** Live and optimisation states are persisted to SQLite for crash recovery.

---

### 8. Cron & Scheduling (`Chronos.Core.Engine.Management.Scheduling`)

| Component | Description |
|-----------|-------------|
| `CronJobManager` | Manages cron jobs and one‑off schedules. |
| **Cron jobs:** | Uses `NCrontab` for parsing; stored in SQLite. |
| **Schedules:** | One‑off timers; stored in SQLite; executed once or repeated. |

**Commands:** `SetCronJob`, `DeleteCronJob`, `ListCronJobs`, `SetSchedule`, `DeleteSchedule`, `ListSchedules`.

---

### 9. BehaviorRecorder (`Chronos.Core.Engine.Services.BehaviorRecorder`)

Sparse behaviour logging for reinforcement learning.

- **Recording:** Buffered records (10,000 threshold) with MessagePack serialisation.
- **Compression:** GZip on disk.
- **Upload:** Periodic upload to Cloud via binary transfer.
- **Cleanup:** Files deleted after successful upload.

**Commands:** `EnableBehaviorLogging`, `DisableBehaviorLogging`, `GetBehaviorLogs`, `DeleteBehaviorLogs`.

---

### 10. Self‑Update (`Chronos.Core.Engine.Services.Update`)

Automatic binary updates via Cloud heartbeat.

**Process:**
1. Cloud includes `NewVersion`, `DownloadUrl`, `Checksum` in heartbeat.
2. Engine downloads and verifies SHA‑256 checksum.
3. Stages new binary in `update/` directory.
4. Writes pending marker (`update.pending`).
5. Launches new binary with `--command=restart`.
6. New binary finalises replacement and resumes operation.

**Rollback:** Old binary kept in `backup/`; rollback on 3 consecutive startup failures.

---

## Directory Structure (Runtime)

```
EngineRoot/
├── Chronos.Core.Engine.exe       (Windows) / Chronos.Core.Engine (Linux)
├── *.dll                         (engine dependencies)
├── Adapters/                     ← Extension DLLs (adapter implementations)
├── Strategies/                   ← Extension DLLs (strategy implementations)
├── Indicators/                   ← Extension DLLs (indicator implementations)
├── Plugins/                      ← Extension DLLs (hook plugin implementations)
├── NeuralNetworks/               ← Extension DLLs (NN model implementations)
├── state/                        ← SQLite state database
├── logs/                         ← Daily rotated JSON logs
├── downloads/                    ← Received binary files
├── backup/                       ← Self‑update backups
├── update/                       ← Staged updates
└── behavior_logs/                ← Compressed behaviour log files
```

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Chronos.Core.Sdk` | Public contracts. |
| `Chronos.Core.Shared` | Memory‑mapped tick lists. |
| `Chronos.Core.Kernel` | Core trading logic. |
| `Microsoft.Extensions.Hosting` | Hosting and service management. |
| `Microsoft.Extensions.DependencyInjection` | DI container. |
| `Microsoft.Data.Sqlite` | State persistence. |
| `Serilog` | Structured logging. |
| `NCrontab` | Cron job scheduling. |
| `MessagePack` | Behaviour log serialisation. |

---

## Build & Deployment

### Prerequisites

- .NET 10 SDK (`10.0.300` or later)
- Windows Server 2019+ or Ubuntu 20.04+

### Build

```bash
cd core
dotnet restore
dotnet build --configuration Release
```

### Deploy

1. Publish to a folder:
   ```bash
   dotnet publish src/Chronos.Core.Engine --configuration Release --output ./publish
   ```
2. Copy the publish folder to the target server.
3. Run the engine or install as a service.

### Run

```bash
# Interactive (prompts for credentials)
./Chronos.Core.Engine

# With credentials
./Chronos.Core.Engine --auth=username,password,apikey

# As a service (Windows)
Chronos.Core.Engine.exe --service --auth=username,password,apikey

# As a service (Linux)
./Chronos.Core.Engine --service --auth=username,password,apikey
```

---

## Configuration

The Engine has **no configuration file**. All operational parameters are supplied by Chronos Cloud after authentication. Local configuration is limited to:

- Credentials (`--auth` or interactive prompt)
- Environment variables:
  - `CHRONOS_PRIMARY_ENDPOINT` – Override primary Cloud endpoint.
  - `CHRONOS_FALLBACK_ENDPOINT` – Override fallback Cloud endpoint.
  - `CHRONOS_AUTH_TOKEN` – Base64‑encoded credentials (alternative to `--auth`).

---

## Security

- **Authentication:** Three‑factor (username + password + instance API key).
- **Encryption:** ECDH key exchange + AES‑256‑GCM.
- **Anti‑tampering:** Binary signing, integrity checks (`SecurityManager.VerifyIntegrity()`).
- **Anti‑debugging:** Runtime checks (`SecurityManager.IsDebuggerAttached()`).
- **Extension security:** Strong‑naming required in production.
- **Credentials:** Encrypted in memory; never persisted to disk.

---

## Logging & Monitoring

### Logs

- **Format:** JSON (CompactJsonFormatter).
- **Rotation:** Daily, 10 MB size limit, 31 days retention.
- **Error logs:** Separate file for errors only.

### Telemetry

- **Engine metrics:** `EngineTelemetry` – connection state, command execution, task counts.
- **Kernel metrics:** `CoreMetrics` – GA fitness, backtest throughput, live order latency.
- **Export:** Available via `GetMetrics` and `ExportMetrics` commands.

---

## Testing

Tests are located in the `tests/` directory:

- **Unit tests:** `Chronos.Core.Sdk.UnitTests` (200+ tests covering SDK contracts).
- **Integration tests:** `Chronos.Core.Sdk.IntegrationTests` (covers file I/O, random, calculators, hooks).

Tests for Kernel and Engine components will be added in future releases.

---

## Future Enhancements

- **Multi‑engine support:** Run multiple isolated engines in the same process.
- **gRPC control API:** Alternative to WebSockets.
- **Prometheus exporter:** Expose metrics via HTTP endpoint.
- **Docker containerisation:** Official Docker images with Helm charts.

---

*This README is intended for Chronos core developers and system administrators. For extension development, see the [Chronos.Core.Sdk README](src/Chronos.Core.Sdk/README.md).*