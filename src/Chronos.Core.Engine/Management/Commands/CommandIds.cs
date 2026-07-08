// -----------------------------------------------------------------------------
// <copyright file="CommandIds.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands;

/// <summary>
/// Central registry of all command IDs used by the engine.
/// </summary>
internal static class CommandIds
{
    // ─── System Management (1000‑1099) ──────────────────────────

    /// <summary>Authenticate command ID (1000).</summary>
    public const int Auth = 1000;
    /// <summary>AuthConfirm command ID (1001).</summary>
    public const int AuthConfirm = 1001;
    /// <summary>Heartbeat command ID (1002).</summary>
    public const int Heartbeat = 1002;
    /// <summary>GetStatus command ID (1003).</summary>
    public const int GetStatus = 1003;
    /// <summary>PauseEngine command ID (1004).</summary>
    public const int PauseEngine = 1004;
    /// <summary>ResumeEngine command ID (1005).</summary>
    public const int ResumeEngine = 1005;
    /// <summary>Shutdown command ID (1006).</summary>
    public const int Shutdown = 1006;
    /// <summary>Restart command ID (1007).</summary>
    public const int Restart = 1007;
    /// <summary>SetConfig command ID (1008).</summary>
    public const int SetConfig = 1008;
    /// <summary>GetConfig command ID (1009).</summary>
    public const int GetConfig = 1009;
    /// <summary>GetCapabilities command ID (1010).</summary>
    public const int GetCapabilities = 1010;

    // ─── Live Trading (1100‑1199) ─────────────────────────────────

    /// <summary>StartLive command ID (1100).</summary>
    public const int StartLive = 1100;
    /// <summary>StopLive command ID (1101).</summary>
    public const int StopLive = 1101;
    /// <summary>InjectGenes command ID (1102).</summary>
    public const int InjectGenes = 1102;
    /// <summary>PauseLive command ID (1103).</summary>
    public const int PauseLive = 1103;
    /// <summary>ResumeLive command ID (1104).</summary>
    public const int ResumeLive = 1104;
    /// <summary>GetLiveState command ID (1105).</summary>
    public const int GetLiveState = 1105;
    /// <summary>SyncLive command ID (1106).</summary>
    public const int SyncLive = 1106;
    /// <summary>SetLiveConfig command ID (1107).</summary>
    public const int SetLiveConfig = 1107;
    /// <summary>GetLiveMetrics command ID (1108).</summary>
    public const int GetLiveMetrics = 1108;

    // ─── Backtesting (1200‑1299) ──────────────────────────────────

    /// <summary>RunBacktest command ID (1200).</summary>
    public const int RunBacktest = 1200;
    /// <summary>CancelBacktest command ID (1201).</summary>
    public const int CancelBacktest = 1201;
    /// <summary>GetBacktestResult command ID (1202).</summary>
    public const int GetBacktestResult = 1202;
    /// <summary>ListBacktests command ID (1203).</summary>
    public const int ListBacktests = 1203;

    // ─── Optimisation (1300‑1399) ─────────────────────────────────

    /// <summary>StartOptimization command ID (1300).</summary>
    public const int StartOptimization = 1300;
    /// <summary>CancelOptimization command ID (1301).</summary>
    public const int CancelOptimization = 1301;
    /// <summary>PauseOptimization command ID (1302).</summary>
    public const int PauseOptimization = 1302;
    /// <summary>ResumeOptimization command ID (1303).</summary>
    public const int ResumeOptimization = 1303;
    /// <summary>GetOptimizationState command ID (1304).</summary>
    public const int GetOptimizationState = 1304;
    /// <summary>GetOptimizationResult command ID (1305).</summary>
    public const int GetOptimizationResult = 1305;
    /// <summary>ListOptimizations command ID (1306).</summary>
    public const int ListOptimizations = 1306;

    // ─── Extensions (1400‑1499) ───────────────────────────────────

    /// <summary>ReloadExtensions command ID (1400).</summary>
    public const int ReloadExtensions = 1400;
    /// <summary>DeployExtension command ID (1401).</summary>
    public const int DeployExtension = 1401;
    /// <summary>RemoveExtension command ID (1402).</summary>
    public const int RemoveExtension = 1402;
    /// <summary>ListExtensions command ID (1403).</summary>
    public const int ListExtensions = 1403;
    /// <summary>ActivateExtensions command ID (1404).</summary>
    public const int ActivateExtensions = 1404;

    // ─── Reports (1500‑1599) ──────────────────────────────────────

    /// <summary>GenerateReport command ID (1500).</summary>
    public const int GenerateReport = 1500;
    /// <summary>GetReport command ID (1501).</summary>
    public const int GetReport = 1501;

    // ─── Logs & Telemetry (1600‑1699) ────────────────────────────

    /// <summary>GetLogs command ID (1600).</summary>
    public const int GetLogs = 1600;
    /// <summary>DeleteLogsAll command ID (1601).</summary>
    public const int DeleteLogsAll = 1601;
    /// <summary>DeleteLogsExpired command ID (1602).</summary>
    public const int DeleteLogsExpired = 1602;
    /// <summary>SetLogLevel command ID (1603).</summary>
    public const int SetLogLevel = 1603;
    /// <summary>GetMetrics command ID (1604).</summary>
    public const int GetMetrics = 1604;
    /// <summary>ExportMetrics command ID (1605).</summary>
    public const int ExportMetrics = 1605;

    // ─── Schedules & Cron (1700‑1799) ────────────────────────────

    /// <summary>SetCronJob command ID (1700).</summary>
    public const int SetCronJob = 1700;
    /// <summary>DeleteCronJob command ID (1701).</summary>
    public const int DeleteCronJob = 1701;
    /// <summary>ListCronJobs command ID (1702).</summary>
    public const int ListCronJobs = 1702;
    /// <summary>SetSchedule command ID (1703).</summary>
    public const int SetSchedule = 1703;
    /// <summary>DeleteSchedule command ID (1704).</summary>
    public const int DeleteSchedule = 1704;
    /// <summary>ListSchedules command ID (1705).</summary>
    public const int ListSchedules = 1705;

    // ─── Admin & Broadcast (1900‑1999) ────────────────────────────

    /// <summary>BroadcastMessage command ID (1900).</summary>
    public const int BroadcastMessage = 1900;
    /// <summary>SetAdminConfig command ID (1901).</summary>
    public const int SetAdminConfig = 1901;
    /// <summary>GetEngineCapabilities command ID (1902).</summary>
    public const int GetEngineCapabilities = 1902;
    /// <summary>GetEngineVersion command ID (1903).</summary>
    public const int GetEngineVersion = 1903;

    // ─── Kill & Emergency (2000‑2099) ────────────────────────────

    /// <summary>KillSwitch command ID (2000).</summary>
    public const int KillSwitch = 2000;
    /// <summary>EmergencyStop command ID (2001).</summary>
    public const int EmergencyStop = 2001;

    // ─── Behaviour Logging (2100‑2199) ───────────────────────────

    /// <summary>EnableBehaviorLogging command ID (2100).</summary>
    public const int EnableBehaviorLogging = 2100;
    /// <summary>DisableBehaviorLogging command ID (2101).</summary>
    public const int DisableBehaviorLogging = 2101;
    /// <summary>GetBehaviorLogs command ID (2102).</summary>
    public const int GetBehaviorLogs = 2102;
    /// <summary>DeleteBehaviorLogs command ID (2103).</summary>
    public const int DeleteBehaviorLogs = 2103;
}
