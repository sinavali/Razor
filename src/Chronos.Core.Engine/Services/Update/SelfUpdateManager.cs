using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Chronos.Core.Engine.Services.Update;

internal interface ISelfUpdateManager
{
    Task<bool> CheckForUpdateAsync(CancellationToken cancellationToken);
    Task InstallUpdateAsync(Uri downloadUrl, string version, string checksum, CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812")]
internal sealed class SelfUpdateManager : ISelfUpdateManager, IDisposable
{
    private readonly ILogger<SelfUpdateManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _backupPath;
    private readonly string _updatePath;
    private bool _disposed;

    private static readonly Action<ILogger, Exception?> _logCheckingForUpdates =
        LoggerMessage.Define(LogLevel.Information, 0, "Checking for updates...");
    private static readonly Action<ILogger, string, Exception?> _logDownloadingUpdate =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Downloading update {Version}...");
    private static readonly Action<ILogger, string, Exception?> _logBackupCreated =
        LoggerMessage.Define<string>(LogLevel.Information, 2, "Backup created: {BackupFile}");
    private static readonly Action<ILogger, string, Exception?> _logUpdateInstalled =
        LoggerMessage.Define<string>(LogLevel.Information, 3, "Update {Version} installed. Restart required.");
    private static readonly Action<ILogger, Exception?> _logRollingBack =
        LoggerMessage.Define(LogLevel.Information, 4, "Rolling back...");
    private static readonly Action<ILogger, Exception?> _logNoBackupFound =
        LoggerMessage.Define(LogLevel.Warning, 5, "No backup found.");
    private static readonly Action<ILogger, Exception?> _logRollbackComplete =
        LoggerMessage.Define(LogLevel.Information, 6, "Rollback complete. Restart required.");

    public SelfUpdateManager(ILogger<SelfUpdateManager> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup");
        _updatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");
        Directory.CreateDirectory(_backupPath);
        Directory.CreateDirectory(_updatePath);
    }

    public async Task<bool> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        _logCheckingForUpdates(_logger, null);
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        return false;
    }

#pragma warning disable CA2007
    public async Task InstallUpdateAsync(Uri downloadUrl, string version, string checksum, CancellationToken cancellationToken)
    {
        _logDownloadingUpdate(_logger, version, null);
        var tempPath = Path.GetTempFileName();
        try
        {
            var response = await _httpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(tempPath);
            await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);

            using var sha = SHA256.Create();
            await using var stream = File.OpenRead(tempPath);
            var hash = Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false)).ToUpperInvariant();
            if (!hash.Equals(checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Checksum mismatch: expected {checksum}, got {hash}.");
            }

            var currentPath = Environment.ProcessPath!;
            var backupFile = Path.Combine(_backupPath, $"engine_{DateTime.UtcNow:yyyyMMddHHmmss}.exe");
            File.Copy(currentPath, backupFile, true);
            _logBackupCreated(_logger, backupFile, null);

            var stagePath = Path.Combine(_updatePath, $"engine_{version}.exe");
            File.Move(tempPath, stagePath, true);
            File.Move(stagePath, currentPath, true);
            _logUpdateInstalled(_logger, version, null);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
#pragma warning restore CA2007

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        _logRollingBack(_logger, null);
        var backups = Directory.GetFiles(_backupPath, "engine_*.exe");
        if (backups.Length == 0)
        {
            _logNoBackupFound(_logger, null);
            return Task.CompletedTask;
        }
        Array.Sort(backups, (a, b) => File.GetCreationTime(b).CompareTo(File.GetCreationTime(a)));
        var latest = backups[0];
        var current = Environment.ProcessPath!;
        File.Copy(latest, current, true);
        _logRollbackComplete(_logger, null);
        return Task.CompletedTask;
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
