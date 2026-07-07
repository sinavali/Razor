# Chronos Installation & Deployment Guide

**Version:** 1.0.0 LTS  
**Audience:** End‑users (traders, quants, IT staff)  
**Status:** Authoritative  
**Last Updated:** 2026-07-07  

---

## 1. Introduction

This guide explains how to install the Chronos Engine on Windows and Linux servers, connect it to Chronos Cloud, and verify correct operation. The engine is a lightweight, headless binary that runs your trading strategies and communicates with your chosen brokers through adapter extensions.

### 1.1 Who Should Read This

- Traders deploying Chronos for live or paper trading.
- IT staff responsible for provisioning and maintaining trading servers.
- Extension developers testing their adapters, strategies, indicators, hook plugins, and neural network models locally.

### 1.2 What You Need

- A server or virtual machine meeting the minimum specifications.
- A Chronos Cloud account (free or paid) with an API key for your engine instance.
- The Chronos Engine binary archive for your operating system.
- Outbound network access to Chronos Cloud and your broker's API.

---

## 2. System Requirements

### 2.1 Minimum Specifications

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| **CPU** | 2 cores | 4+ cores (for optimizations) |
| **RAM** | 4 GB | 8+ GB (for large tick datasets) |
| **Disk** | 10 GB free | SSD with 50+ GB free |
| **Network** | 1 Mbps outbound | 10+ Mbps outbound |
| **OS** | Windows Server 2019+ / Ubuntu 20.04+ | Latest LTS |

### 2.2 Supported Operating Systems

- **Windows** – x64, Windows 10/11, Windows Server 2019 or later.
- **Linux** – x64, Debian 11+, Ubuntu 20.04+, or any modern distribution with .NET 10 runtime installed.

**Important platform note for MetaTrader 5 adapters:** MetaTrader 5 provides only Windows DLLs. MT5 adapters **cannot run on Linux**. If your trading workflow requires MetaTrader 5, you must deploy the Chronos Engine on a Windows server. Other broker APIs (cTrader, Binance, Interactive Brokers, etc.) are typically cross‑platform.

### 2.3 .NET Runtime

The Chronos Engine targets .NET 10. The correct runtime is bundled with the engine archive; you do not need to install .NET separately unless you are building extensions from source.

---

## 3. Getting Your Engine Binary

### 3.1 Download

1. Log in to Chronos Cloud.
2. Navigate to **Engines** → **Download Engine**.
3. Select your operating system (Windows or Linux).
4. Download the compressed archive.

### 3.2 Archive Contents

The archive contains:

```
chronos/
├── Chronos.Engine.exe       (Windows) / Chronos.Engine (Linux)
├── *.dll                    (engine dependencies)
├── Adapters/                (empty – place adapter DLLs here)
├── Strategies/              (empty – place strategy DLLs here)
├── Indicators/              (empty – place indicator DLLs here)
├── Plugins/                 (empty – place hook plugin DLLs here)
├── NeuralNetworks/          (empty – place NN model DLLs here)
└── logs/                    (created on first run)
```

**There is no configuration file in the archive.** All operational parameters are supplied by Chronos Cloud after authentication.

---

## 4. Installation Steps

### 4.1 Windows Installation

1. **Extract the archive** to a permanent location, e.g., `C:\Chronos\`.
2. **Place extensions** – copy your adapter, strategy, indicator, hook plugin, and NN model DLLs into the appropriate directories (see §8 for details).
3. **Run the engine**:
   - Open a **Command Prompt** or **PowerShell** as Administrator.
   - Navigate to `C:\Chronos\`.
   - Run: `.\Chronos.Engine.exe`
   - On first startup, the engine will prompt you for your **Cloud Username**, **Cloud Password**, and **Instance API Key**. Enter them interactively.
   - For automated or service deployments, you can pass credentials via the `--auth` flag (see §4.3).

### 4.2 Linux Installation

1. **Extract the archive** to `/opt/chronos/`:
   ```bash
   sudo mkdir -p /opt/chronos
   sudo tar -xzf chronos-linux-x64.tar.gz -C /opt/chronos
   ```
2. **Set permissions**:
   ```bash
   sudo chmod +x /opt/chronos/Chronos.Engine
   ```
3. **Place extensions** in the appropriate subdirectories under `/opt/chronos/`.
4. **Run the engine**:
   ```bash
   cd /opt/chronos
   ./Chronos.Engine
   ```
   - If you are running interactively, the engine will prompt for credentials.
   - For background or service operation, use the `--auth` flag as described below.

### 4.3 Authentication Options

Credentials are **never stored on disk**. They are held in memory only for the duration of the session.

**Option 1: Interactive Prompt** (default)

- Run the engine with no arguments. It will display:
  ```
  === Engine Authentication ===
  Cloud Username: 
  Cloud Password: 
  Instance API Key: 
  ```
- Enter your credentials. They are validated against Chronos Cloud and then used to establish the secure session.

**Option 2: Command‑line `--auth` flag** (for automation)

- Use the following syntax:
  ```bash
  Chronos.Engine.exe --auth=username,password,apikey
  ```
- The three values must be comma‑separated, with no spaces.
- **Security warning:** The command line is visible to other processes and may be stored in shell history. Use this only in secure, controlled environments. For production services, ensure that the command line is not logged.

**Option 3: Environment variable** (recommended for services)

- Set the environment variable `CHRONOS_AUTH_TOKEN` to a base64‑encoded string of `username:password:apikey`.
- The engine reads this variable on startup if the `--auth` flag is not provided.

### 4.4 Running as a Service

The engine can be installed as a background service on both Windows and Linux.

#### Windows Service

1. Install the engine binary in a permanent directory, e.g., `C:\Chronos`.
2. Create a service using `sc`:
   ```
   sc create ChronosEngine binPath = "C:\Chronos\Chronos.Engine.exe --service --auth=username,password,apikey" start=auto
   ```
3. Start the service:
   ```
   sc start ChronosEngine
   ```
4. Monitor logs in `C:\Chronos\logs\`.

#### Linux systemd Service

1. Install the engine binary in `/opt/chronos`.
2. Create `/etc/systemd/system/chronos.service`:
   ```ini
   [Unit]
   Description=Chronos Engine
   After=network.target

   [Service]
   ExecStart=/opt/chronos/Chronos.Engine --service --auth=username,password,apikey
   WorkingDirectory=/opt/chronos
   Restart=on-failure
   RestartSec=10
   User=chronos
   Group=chronos
   StandardOutput=append:/opt/chronos/logs/stdout.log
   StandardError=append:/opt/chronos/logs/stderr.log

   [Install]
   WantedBy=multi-user.target
   ```
3. Enable and start:
   ```
   sudo systemctl daemon-reload
   sudo systemctl enable chronos
   sudo systemctl start chronos
   ```

> **Note:** Replace `username,password,apikey` with the actual credentials. The `--service` flag tells the engine to run as a daemon/service.

---

## 5. Obtaining an Instance API Key

1. In Chronos Cloud, go to **Engines** → **Register New Engine**.
2. Give the engine a friendly name.
3. Copy the generated API key.
4. Use this key as the third component of the `--auth` flag or enter it when prompted.

Never share this key. If compromised, revoke it in the Cloud dashboard and generate a new one.

---

## 6. Network Requirements

### 6.1 Outbound Connectivity

The engine must be able to establish outbound WebSocket connections to:

- **Chronos Cloud** – the hardcoded endpoint `wss://cloud.chronos.io/engine` (with a fallback to `wss://cloud.chronos-fallback.io/engine`).
- **Your broker's API** – whatever host/port your adapter requires.

The engine does **not** listen on any inbound port; it initiates all connections.

### 6.2 Firewall Configuration

Ensure your firewall allows outbound TCP traffic on:
- Port 443 (HTTPS/WebSocket Secure) for Chronos Cloud.
- Any ports required by your broker adapter.

No inbound ports need to be opened.

### 6.3 Proxy Support

If your network requires a proxy, contact Chronos support. Explicit proxy configuration will be added in a future release.

---

## 7. Verifying the Installation

### 7.1 Engine Startup

When the engine starts successfully, you will see log output similar to:

```
[info] Chronos Engine v1.0.0 LTS starting...
[info] Discovered: 2 adapters, 3 strategies, 4 indicators, 1 NN model, 5 hook plugins
[info] Connecting to Chronos Cloud...
[info] Connected and authenticated. Engine ID: eng_abc123
[info] Sending extension manifest to Cloud...
[info] Cloud activated: adapter=BinanceAdapter, strategy=MACrossover, indicators=[SMA,RSI], plugins=[DrawdownGuard,TelegramNotifier], nnModel=none
[info] Waiting for commands...
```

### 7.2 Cloud Verification

In Chronos Cloud, navigate to **Engines**. Your engine should appear as **Online** with a green indicator. If it shows **Offline** or **Error**, check the local log file in the `logs/` directory.

### 7.3 Running a Test Backtest

1. In Chronos Cloud, create a simple strategy configuration.
2. Click **Run Backtest** and select your online engine.
3. The engine processes the backtest and streams progress to the Cloud.
4. Once complete, view the results in the Cloud dashboard.

This confirms end‑to‑end connectivity and correct engine operation.

---

## 8. Extension Management

### 8.1 Directory Structure

Extensions are placed in subdirectories alongside the engine executable:

| Directory | Purpose | Implements |
|-----------|---------|------------|
| `Adapters/` | Broker/exchange connectivity | `IAdapterCapability` |
| `Strategies/` | Trading logic | `IStrategyCapability` |
| `Indicators/` | Technical analysis computations | `Indicator` (abstract base) |
| `Plugins/` | Hook‑based extensions | `IHookManifest` |
| `NeuralNetworks/` | Neural network models | `INeuralNetworkModel` |

A single DLL can be placed in any directory—the engine scans all of them. However, for organisational clarity, each extension type has its own directory.

### 8.2 Single‑File vs Multi‑File Extensions

**Single‑file extensions:** Place the `.dll` directly in the appropriate directory.
```
Adapters/
├── BinanceAdapter.dll
└── NobitexAdapter.dll
```

**Multi‑file extensions:** Create a subfolder with the extension's name, containing all required DLLs. The engine scans for the DLL matching the folder name.
```
Adapters/
└── NobitexAdapter/
    ├── NobitexAdapter.dll      ← scanned
    └── NobitexApiClient.dll    ← loaded as dependency
```

### 8.3 Discovery and Activation

1. On startup, the engine scans all extension directories.
2. It builds a manifest of discovered adapters, strategies, indicators, hook plugins, and NN models.
3. The manifest is sent to Chronos Cloud.
4. The user selects active items from their Cloud profile.
5. The Cloud sends the active set back to the engine.
6. The engine activates the selected adapter, strategy, indicators, and plugins. Inactive items are not loaded.

### 8.4 Hot‑Reloading Extensions

When the Cloud sends a `ReloadExtensions` command:

1. The engine completes the current task (backtest/live/optimization) naturally.
2. It unloads all extension assembly contexts.
3. It rescans all directories and sends a new manifest to the Cloud.
4. The Cloud responds with the new active set.
5. The engine activates the new extensions and is ready for new commands.

The engine process never stops—only the task pipeline pauses briefly.

---

## 9. Updating the Engine

Chronos Cloud notifies you when a new engine version is available. To update:

1. In the Cloud, go to **Engines** → select your engine → **Update Engine**.
2. The engine downloads the new binary, verifies its cryptographic signature, and schedules a restart.
3. If the engine is live, it will close all positions (per your settings) before restarting.

Manual update: download the new archive and replace the files, preserving your extension directories.

---

## 10. Security Considerations

### 10.1 Protect Your Credentials

- Never share your Instance API Key.
- Use the interactive prompt or environment variable for authentication in production; avoid `--auth` where command‑line visibility is a concern.
- Credentials are held only in memory and are never persisted to disk.

### 10.2 Secrets Management

Broker API keys and other secrets are stored in Chronos Cloud, not on the engine. The engine fetches them securely over the encrypted channel. A local encrypted secrets file may be used as a cache but is not the primary store.

---

## 11. Logs and Troubleshooting

### 11.1 Log Files

Logs are written to the `logs/` directory with daily rotation:
```
logs/chronos-20260707.log
logs/chronos-20260706.log
...
```

### 11.2 Common Issues

| Problem | Likely Cause | Solution |
|---------|-------------|----------|
| Engine exits immediately | Invalid `--auth` format or missing credentials | Verify the format or run interactively. |
| Engine cannot connect to Cloud | Firewall blocking outbound | Allow outbound TCP 443. |
| Engine shows "Invalid API key" | Key revoked or mistyped | Regenerate key in Cloud, update credentials. |
| Extensions not loaded | Missing `SdkVersion` attribute or mismatched version | Check assembly attributes. |
| Extension rejected – SDK major version mismatch | Extension compiled against a different SDK major | Recompile extension against the matching SDK. |
| MT5 adapter fails on Linux | MT5 is Windows‑only | Deploy engine on Windows. |
| High CPU on backtest | Normal (heavy workload) | Tune `MaxParallelThreads` in execution spec. |

### 11.3 Getting Support

Send the relevant log excerpts to Chronos support through the Cloud dashboard. Do not share your credentials.

---

## 12. Uninstalling

1. Stop the engine (Ctrl+C or `systemctl stop chronos`).
2. Delete the engine directory.
3. Revoke the engine's API key in Chronos Cloud.
4. Remove the engine from the Cloud dashboard.