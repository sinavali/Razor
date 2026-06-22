namespace Chronos.Core.Engine.Core;

/// <summary>
/// Application‑wide constants.
/// </summary>
internal static class AppConstants
{
    /// <summary>Primary Cloud endpoint (can be overridden by environment variable).</summary>
    public static string PrimaryEndpoint => Environment.GetEnvironmentVariable("CHRONOS_PRIMARY_ENDPOINT") ?? "wss://cloud.chronos.io/engine";

    /// <summary>Fallback Cloud endpoint (can be overridden by environment variable).</summary>
    public static string FallbackEndpoint => Environment.GetEnvironmentVariable("CHRONOS_FALLBACK_ENDPOINT") ?? "wss://cloud.chronos-fallback.io/engine";

    /// <summary>Default grace period in hours.</summary>
    public const int DefaultGracePeriodHours = 3;

    /// <summary>Default heartbeat interval in seconds.</summary>
    public const int DefaultHeartbeatIntervalSeconds = 10;

    /// <summary>Default chunk size for binary transfers in bytes.</summary>
    public const int DefaultChunkSize = 64 * 1024;

    /// <summary>Default interval for uploading behaviour logs in seconds.</summary>
    public const int DefaultBehaviorUploadIntervalSeconds = 60;

    /// <summary>Engine version.</summary>
    public const string EngineVersion = "1.0.0";

    /// <summary>SDK version.</summary>
    public const string SdkVersion = "1.0.0";

    public static bool IsDevelopment { get; set; }

    // Retry settings
    public const int DefaultRetryDelaySeconds = 1;
    public const int MaxRetryDelaySeconds = 60;

    /// <summary>Supported feature IDs (capabilities).</summary>
    public static readonly int[] SupportedCapabilities =
    [
        100, // Live Trading
        101, // Backtesting
        102, // Optimisation
        103, // Neural Networks
        104, // Hooks
        105, // Cronjobs
        106, // Schedules
        107, // Mining
        108, // Self-Update
        109, // Log Streaming
        110, // Telemetry Export
        111  // Behavior Logging
    ];
}
