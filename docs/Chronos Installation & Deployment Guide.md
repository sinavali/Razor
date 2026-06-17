## Chronos Installation & Deployment Guide

**Version:** 1.0.0 LTS
**Audience:** End‑users (traders, quants, IT staff)
**Status:** Authoritative
**Last Updated:** 2026-06-14

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
├── chronos.bootstrap.json   (template – edit before running)
├── Adapters/                (empty – place adapter DLLs here)
├── Strategies/              (empty – place strategy DLLs here)
├── Indicators/              (empty – place indicator DLLs here)
├── Plugins/                 (empty – place hook plugin DLLs here)
├── NeuralNetworks/          (empty – place NN model DLLs here)
└── logs/                    (created on first run)
```

---

## 4. Installation Steps

### 4.1 Windows Installation

1. **Extract the archive** to a permanent location, e.g., `C:\Chronos\`.
2. **Edit the bootstrap file** `chronos.bootstrap.json` (see §5).
3. **Place extensions** – copy your adapter, strategy, indicator, hook plugin, and NN model DLLs into the appropriate directories (see §8 for details).
4. **Run the engine**:
   - Open a **Command Prompt** or **PowerShell** as Administrator.
   - Navigate to `C:\Chronos\`.
   - Run: `.\Chronos.Engine.exe`
   - The engine will start, authenticate with Chronos Cloud, and display a live log stream.

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
3. **Edit the bootstrap file** `/opt/chronos/chronos.bootstrap.json` (see §5).
4. **Place extensions** in the appropriate subdirectories under `/opt/chronos/`.
5. **Run the engine**:
   ```bash
   cd /opt/chronos
   ./Chronos.Engine
   ```

### 4.3 Running as a Service (Linux)

To run the engine as a systemd service:

1. Create `/etc/systemd/system/chronos.service`:
   ```ini
   [Unit]
   Description=Chronos Engine
   After=network.target

   [Service]
   ExecStart=/opt/chronos/Chronos.Engine
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

2. Enable and start:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable chronos
   sudo systemctl start chronos
   ```

3. Check status: `sudo systemctl status chronos`

---

## 5. Bootstrap Configuration

The bootstrap file `chronos.bootstrap.json` contains the minimal settings the engine needs to connect to Chronos Cloud.

### 5.1 File Format

```json
{
    "CloudEndpoint": "wss://cloud.chronos.io/engine",
    "InstanceApiKey": "ck_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "EncryptionSeed": "a 32-byte base64-encoded seed for AES key derivation",
    "LogVerbosity": "Information"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `CloudEndpoint` | Yes | WebSocket URL of Chronos Cloud. |
| `InstanceApiKey` | Yes | API key from Chronos Cloud (Engine registration page). |
| `EncryptionSeed` | Yes | A secret string used to derive session encryption keys. Keep it secret and identical across engine restarts. |
| `LogVerbosity` | No | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`. Default: `Information`. |

### 5.2 Obtaining the API Key

1. In Chronos Cloud, go to **Engines** → **Register New Engine**.
2. Give the engine a friendly name.
3. Copy the generated API key.
4. Paste it into the `InstanceApiKey` field.

Never share this key. If compromised, revoke it in the Cloud dashboard and generate a new one.

---

## 6. Network Requirements

### 6.1 Outbound Connectivity

The engine must be able to establish outbound WebSocket connections to:

- **Chronos Cloud** – the endpoint in your bootstrap file (default `wss://cloud.chronos.io`).
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

A single DLL can be placed in any directory—the engine scans all of them. However, for organizational clarity, each extension type has its own directory.

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

Manual update: download the new archive and replace the files, preserving your `chronos.bootstrap.json` and extension directories.

---

## 10. Security Considerations

### 10.1 Protect the Bootstrap File

The bootstrap file contains your API key and encryption seed. Restrict file permissions:

- **Windows:** `icacls chronos.bootstrap.json /inheritance:r /grant:r "SYSTEM:(R)" /grant:r "Administrators:(R)"`
- **Linux:** `chmod 600 /opt/chronos/chronos.bootstrap.json`

### 10.2 Secrets Management

Broker API keys are stored in Chronos Cloud, not in the bootstrap file. The engine fetches them securely over the encrypted channel. A local encrypted secrets file may be used as a cache but is not the primary store.

---

## 11. Logs and Troubleshooting

### 11.1 Log Files

Logs are written to the `logs/` directory with daily rotation:
```
logs/chronos-20260614.log
logs/chronos-20260613.log
...
```

### 11.2 Common Issues

| Problem | Likely Cause | Solution |
|---------|-------------|----------|
| Engine exits immediately | Invalid bootstrap JSON | Validate JSON syntax. |
| Engine cannot connect to Cloud | Firewall blocking outbound | Allow outbound TCP 443. |
| Engine shows "Invalid API key" | Key revoked or mistyped | Regenerate key in Cloud, update bootstrap. |
| Extensions not loaded | Missing `ChronosSdkVersion` attribute or mismatched version | Check assembly attributes. |
| Extension rejected – SDK major version mismatch | Extension compiled against a different SDK major | Recompile extension against the matching SDK. |
| MT5 adapter fails on Linux | MT5 is Windows‑only | Deploy engine on Windows. |
| High CPU on backtest | Normal (heavy workload) | Tune `MaxParallelThreads` in execution spec. |

### 11.3 Getting Support

Send the relevant log excerpts to Chronos support through the Cloud dashboard. Do not share your bootstrap file or API key.

---

## 12. Uninstalling

1. Stop the engine (Ctrl+C or `systemctl stop chronos`).
2. Delete the engine directory.
3. Revoke the engine's API key in Chronos Cloud.
4. Remove the engine from the Cloud dashboard.

---

*Ready for the next document.*