using Chronos.Core.Engine.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace Chronos.Core.Engine.Services.Update;

internal interface ISelfUpdateManager
{
    bool IsUpdateAvailable { get; }
    (string Version, Uri DownloadUrl, string Checksum)? PendingUpdate { get; }
    void CheckForUpdate(string version, Uri downloadUrl, string checksum);
    Task InstallUpdateAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
    Task FinalizeUpdateAsync(CancellationToken cancellationToken);
}

internal sealed class SelfUpdateManager : ISelfUpdateManager, IDisposable
{
    private readonly ILogger<SelfUpdateManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _backupPath;
    private readonly string _updatePath;
    private readonly string _currentPath;
    private readonly string _executableName;
    private bool _disposed;

    private string? _pendingVersion;
    private Uri? _pendingDownloadUrl;
    private string? _pendingChecksum;

    private static readonly Action<ILogger, Exception?> _logCheckingForUpdates =
        LoggerMessage.Define(LogLevel.Information, 0, "Checking for updates...");
    private static readonly Action<ILogger, string, Exception?> _logDownloadingUpdate =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Downloading update {Version}...");
    private static readonly Action<ILogger, string, Exception?> _logBackupCreated =
        LoggerMessage.Define<string>(LogLevel.Information, 2, "Backup created: {BackupFile}");
    private static readonly Action<ILogger, string, Exception?> _logUpdateStaged =
        LoggerMessage.Define<string>(LogLevel.Information, 3, "Update {Version} staged. Restart required.");
    private static readonly Action<ILogger, Exception?> _logRollingBack =
        LoggerMessage.Define(LogLevel.Information, 4, "Rolling back...");
    private static readonly Action<ILogger, Exception?> _logNoBackupFound =
        LoggerMessage.Define(LogLevel.Warning, 5, "No backup found.");
    private static readonly Action<ILogger, Exception?> _logRollbackComplete =
        LoggerMessage.Define(LogLevel.Information, 6, "Rollback complete. Restart required.");
    private static readonly Action<ILogger, string, Exception?> _logUpdateInstalled =
        LoggerMessage.Define<string>(LogLevel.Information, 7, "Update {Version} installed successfully.");
    private static readonly Action<ILogger, Exception?> _logFinalizingUpdate =
        LoggerMessage.Define(LogLevel.Information, 8, "Finalizing update (--command=restart)...");
    private static readonly Action<ILogger, string, Exception?> _logUpdateFinalized =
        LoggerMessage.Define<string>(LogLevel.Information, 9, "Update finalized. New version: {Version}");
    private static readonly Action<ILogger, Exception?> _logNoPendingMarker =
        LoggerMessage.Define(LogLevel.Warning, 10, "No pending update marker found.");
    private static readonly Action<ILogger, Exception?> _logInvalidMarkerContent =
        LoggerMessage.Define(LogLevel.Warning, 11, "Invalid marker content.");
    private static readonly Action<ILogger, string, Exception?> _logNoStagedBinary =
        LoggerMessage.Define<string>(LogLevel.Warning, 12, "No staged binary found for version {Version}.");
    private static readonly Action<ILogger, Exception?> _logUpdateNotNeeded =
        LoggerMessage.Define(LogLevel.Information, 13, "Update version is same as current. No update needed.");
    private static readonly Action<ILogger, Exception?> _logInvalidUpdateMetadata =
        LoggerMessage.Define(LogLevel.Warning, 14, "Invalid update metadata received.");
    private static readonly Action<ILogger, string, Exception?> _logMarkerWritten =
        LoggerMessage.Define<string>(LogLevel.Information, 15, "Pending marker written: {MarkerPath}");

    public bool IsUpdateAvailable => _pendingVersion != null;
    public (string Version, Uri DownloadUrl, string Checksum)? PendingUpdate =>
        _pendingVersion != null ? (_pendingVersion, _pendingDownloadUrl!, _pendingChecksum!) : null;

    public SelfUpdateManager(ILogger<SelfUpdateManager> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup");
        _updatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");
        _currentPath = Environment.ProcessPath!;
        _executableName = Path.GetFileName(_currentPath);
        Directory.CreateDirectory(_backupPath);
        Directory.CreateDirectory(_updatePath);
    }

    public void CheckForUpdate(string version, Uri downloadUrl, string checksum)
    {
        if (string.IsNullOrWhiteSpace(version) || downloadUrl == null || string.IsNullOrWhiteSpace(checksum))
        {
            _logInvalidUpdateMetadata(_logger, null);
            return;
        }

        if (string.Equals(version, AppConstants.EngineVersion, StringComparison.OrdinalIgnoreCase))
        {
            _logUpdateNotNeeded(_logger, null);
            _pendingVersion = null;
            return;
        }

        _pendingVersion = version;
        _pendingDownloadUrl = downloadUrl;
        _pendingChecksum = checksum;
        _logCheckingForUpdates(_logger, null);
    }

    public async Task InstallUpdateAsync(CancellationToken cancellationToken)
    {
        if (_pendingVersion == null || _pendingDownloadUrl == null || _pendingChecksum == null)
        {
            throw new InvalidOperationException("No update available. Call CheckForUpdate first.");
        }

        _logDownloadingUpdate(_logger, _pendingVersion, null);

        string? tempPath = Path.GetTempFileName();
        bool shouldDeleteTemp = true;
        try
        {
            // Download
            var response = await _httpClient.GetAsync(_pendingDownloadUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using (var fs = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            // Verify checksum
            using var sha = SHA256.Create();
            using (var stream = File.OpenRead(tempPath))
            {
                string hash = Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false));
                if (!hash.Equals(_pendingChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Checksum mismatch: expected {_pendingChecksum}, got {hash}.");
                }
            }

            // RUN‑04: Create a unique backup path and copy current binary to it.
            string backupTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string backupFile = Path.Combine(_backupPath, $"engine_backup_{backupTimestamp}_{_executableName}");
            File.Copy(_currentPath, backupFile, true);
            _logBackupCreated(_logger, backupFile, null);

            // Stage new binary
            string stageFile = Path.Combine(_updatePath, $"engine_{_pendingVersion}_{_executableName}");
            File.Move(tempPath, stageFile, true);
            shouldDeleteTemp = false;

            // Write pending marker with original path and backup path
            string markerPath = Path.Combine(_updatePath, "update.pending");
            // Store version, original path, backup path, and timestamp
            await File.WriteAllTextAsync(markerPath, $"{_pendingVersion}|{_currentPath}|{backupFile}|{DateTime.UtcNow:O}", cancellationToken).ConfigureAwait(false);
            _logMarkerWritten(_logger, markerPath, null);

            _logUpdateStaged(_logger, _pendingVersion, null);

            // Launch new binary with --command=restart
            string args = $"--command=restart --auth={Credentials.Username},{Credentials.Password},{Credentials.InstanceApiKey}";
            var startInfo = new ProcessStartInfo
            {
                FileName = stageFile,
                Arguments = args,
                UseShellExecute = false,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start new binary.");
            }

            Environment.Exit(0);
        }
        finally
        {
            if (shouldDeleteTemp && tempPath != null && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public async Task FinalizeUpdateAsync(CancellationToken cancellationToken)
    {
        _logFinalizingUpdate(_logger, null);

        string markerPath = Path.Combine(_updatePath, "update.pending");
        if (!File.Exists(markerPath))
        {
            _logNoPendingMarker(_logger, null);
            return;
        }

        string markerContent = await File.ReadAllTextAsync(markerPath, cancellationToken).ConfigureAwait(false);
        string[] parts = markerContent.Split('|');
        if (parts.Length < 2)
        {
            _logInvalidMarkerContent(_logger, null);
            return;
        }

        string version = parts[0];
        string originalPath = parts[1];
        string backupPath = parts.Length > 2 ? parts[2] : string.Empty;

        // Find staged binary matching version
        string stagedPattern = $"engine_{version}_*";
        var stagedFiles = Directory.GetFiles(_updatePath, stagedPattern);
        if (stagedFiles.Length == 0)
        {
            _logNoStagedBinary(_logger, version, null);
            return;
        }

        string stagedFile = stagedFiles[0];

        // Replace the original executable with the staged binary atomically.
        // File.Replace creates a backup of the original; we already have a backup, so we can just overwrite.
        File.Replace(stagedFile, originalPath, null, true);
        _logUpdateInstalled(_logger, version, null);

        // Delete marker
        File.Delete(markerPath);

        _logUpdateFinalized(_logger, version, null);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        _logRollingBack(_logger, null);

        // Find the most recent backup file.
        var backups = Directory.GetFiles(_backupPath, $"engine_backup_*_{_executableName}");
        if (backups.Length == 0)
        {
            _logNoBackupFound(_logger, null);
            return;
        }
        // Sort by creation time descending.
        Array.Sort(backups, (a, b) => File.GetCreationTime(b).CompareTo(File.GetCreationTime(a)));
        string latest = backups[0];

        File.Copy(latest, _currentPath, true);
        _logRollbackComplete(_logger, null);

        // Clean up old backup files (keep only the last 5).
        var allBackups = Directory.GetFiles(_backupPath, $"engine_backup_*_{_executableName}")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Skip(5);
        foreach (var file in allBackups)
        {
            try { File.Delete(file); } catch { }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }
}
