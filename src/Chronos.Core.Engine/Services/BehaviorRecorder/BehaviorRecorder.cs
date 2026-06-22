using System.Collections.Concurrent;
using System.IO.Compression;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Services.BehaviorRecorder;

internal interface IBehaviorRecorder
{
    void Enable(string sessionId, string strategyName, double[] genes, int uploadIntervalSeconds = AppConstants.DefaultBehaviorUploadIntervalSeconds);
    bool IsEnabled { get; }
    void Disable();
    void Record(BehaviorRecord record);
    Task FlushAsync(CancellationToken cancellationToken);
    Task<object> GetLogsAsync(string sessionId, CancellationToken cancellationToken);
    Task DeleteLogsAsync(string sessionId, CancellationToken cancellationToken);
}

[MessagePackObject(AllowPrivate = true)]
internal sealed class BehaviorRecord
{
    [Key(0)] public DateTime TimestampUtc { get; set; }
    [Key(1)] public string SessionId { get; set; } = string.Empty;
    [Key(2)] public Dictionary<string, double> State { get; set; } = new();
    [Key(3)] public string Action { get; set; } = string.Empty;
    [Key(4)] public double? Reward { get; set; }
    [Key(5)] public string StrategyName { get; set; } = string.Empty;
}

internal sealed class BehaviorRecorder : IBehaviorRecorder, IDisposable
{
    private readonly ILogger<BehaviorRecorder> _logger;
    private readonly ICloudConnector _cloudConnector;
    private readonly string _baseDirectory;
    private readonly ConcurrentQueue<BehaviorRecord> _buffer = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private Timer? _uploadTimer;
    private bool _isEnabled;
    private string _sessionId = string.Empty;
    private string _strategyName = string.Empty;
    private double[] _genes = Array.Empty<double>();
    private int _recordCount;
    private readonly int _flushThreshold = 10000;
    private bool _disposed;
    private int _uploadIntervalSeconds = AppConstants.DefaultBehaviorUploadIntervalSeconds;

    private static readonly Action<ILogger, string, Exception?> _logBehaviorLoggingEnabled =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Behavior logging enabled for session {SessionId}.");
    private static readonly Action<ILogger, Exception?> _logBehaviorLoggingDisabled =
        LoggerMessage.Define(LogLevel.Information, 1, "Behavior logging disabled.");
    private static readonly Action<ILogger, int, string, Exception?> _logFlushedRecords =
        LoggerMessage.Define<int, string>(LogLevel.Debug, 2, "Flushed {Count} records to {FilePath}.");
    private static readonly Action<ILogger, string, Exception?> _logDeletedBehaviorLog =
        LoggerMessage.Define<string>(LogLevel.Information, 3, "Deleted behavior log: {File}");
    private static readonly Action<ILogger, string, Exception?> _logUploadingBehaviorLog =
        LoggerMessage.Define<string>(LogLevel.Information, 4, "Uploading behavior log: {File}");
    private static readonly Action<ILogger, string, Exception?> _logUploadedBehaviorLog =
        LoggerMessage.Define<string>(LogLevel.Information, 5, "Uploaded behavior log: {File}");
    private static readonly Action<ILogger, string, Exception?> _logUploadBehaviorFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 6, "Failed to upload behavior log: {File}");
    private static readonly Action<ILogger, Exception?> _logUploadTimerStarted =
        LoggerMessage.Define(LogLevel.Information, 8, "Behavior log upload timer started.");
    private static readonly Action<ILogger, Exception?> _logUploadTimerStopped =
        LoggerMessage.Define(LogLevel.Information, 9, "Behavior log upload timer stopped.");

    public bool IsEnabled => _isEnabled;

    public BehaviorRecorder(ILogger<BehaviorRecorder> logger, ICloudConnector cloudConnector)
    {
        _logger = logger;
        _cloudConnector = cloudConnector;
        _baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "behavior_logs");
        Directory.CreateDirectory(_baseDirectory);
        _flushTimer = new Timer(async _ => await FlushAsync(CancellationToken.None).ConfigureAwait(false), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void Enable(string sessionId, string strategyName, double[] genes, int uploadIntervalSeconds = AppConstants.DefaultBehaviorUploadIntervalSeconds)
    {
        _sessionId = sessionId;
        _strategyName = strategyName;
        _genes = genes ?? Array.Empty<double>();
        _uploadIntervalSeconds = uploadIntervalSeconds > 0 ? uploadIntervalSeconds : AppConstants.DefaultBehaviorUploadIntervalSeconds;
        _isEnabled = true;
        _recordCount = 0;
        _logBehaviorLoggingEnabled(_logger, sessionId, null);

        // Start upload timer
        StartUploadTimer();
    }

    public void Disable()
    {
        if (!_isEnabled)
        {
            return;
        }

        _isEnabled = false;
        _logBehaviorLoggingDisabled(_logger, null);
        FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        StopUploadTimer();
    }

    private void StartUploadTimer()
    {
        if (_uploadTimer != null)
        {
            return;
        }

        _uploadTimer = new Timer(
            async _ => await UploadLogsAsync(CancellationToken.None).ConfigureAwait(false),
            null,
            TimeSpan.FromSeconds(_uploadIntervalSeconds),
            TimeSpan.FromSeconds(_uploadIntervalSeconds));
        _logUploadTimerStarted(_logger, null);
    }

    private void StopUploadTimer()
    {
        if (_uploadTimer == null)
        {
            return;
        }

        _uploadTimer.Dispose();
        _uploadTimer = null;
        _logUploadTimerStopped(_logger, null);
    }

    private async Task UploadLogsAsync(CancellationToken cancellationToken)
    {
        if (!_isEnabled)
        {
            return;
        }

        if (!_cloudConnector.IsConnected)
        {
            // Log only once per invocation to avoid spam; we'll log per file below.
            return;
        }

        // Get all behavior log files for the current session
        var files = Directory.GetFiles(_baseDirectory, $"behavior_{_sessionId}_*.bin.gz");
        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested || !_isEnabled)
            {
                break;
            }

            // Skip files that are currently being written? We rely on the fact that FlushAsync closes the file.
            // We'll upload and delete.
            try
            {
                _logUploadingBehaviorLog(_logger, Path.GetFileName(file), null);
                await _cloudConnector.SendBinaryAsync(file, "application/octet-stream", cancellationToken).ConfigureAwait(false);
                // Delete on success
                File.Delete(file);
                _logUploadedBehaviorLog(_logger, Path.GetFileName(file), null);
            }
            catch (Exception ex)
            {
                _logUploadBehaviorFailed(_logger, Path.GetFileName(file), ex);
                // Continue with other files
            }
        }
    }

    public void Record(BehaviorRecord record)
    {
        if (!_isEnabled)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(record);

        record.SessionId = _sessionId;
        record.StrategyName = _strategyName;
        record.TimestampUtc = DateTime.UtcNow;
        _buffer.Enqueue(record);
        if (Interlocked.Increment(ref _recordCount) >= _flushThreshold)
        {
            _ = FlushAsync(CancellationToken.None);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_buffer.IsEmpty)
        {
            return;
        }

        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = new List<BehaviorRecord>();
            while (_buffer.TryDequeue(out var r))
            {
                records.Add(r);
            }

            if (records.Count == 0)
            {
                return;
            }

            var filePath = Path.Combine(_baseDirectory, $"behavior_{_sessionId}_{DateTime.UtcNow:yyyy-MM-dd}.bin.gz");
            var data = MessagePackSerializer.Serialize(records, cancellationToken: cancellationToken);
#pragma warning disable CA2007
            await using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None);
            await using var gz = new GZipStream(fs, CompressionLevel.Optimal);
#pragma warning restore CA2007
            await gz.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            _recordCount = 0;
            _logFlushedRecords(_logger, records.Count, filePath, null);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task<object> GetLogsAsync(string sessionId, CancellationToken cancellationToken)
    {
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        var files = Directory.GetFiles(_baseDirectory, $"behavior_{sessionId}_*.bin.gz");
        var logFiles = new List<object>();

        foreach (var file in files)
        {
            logFiles.Add(new
            {
                FileName = Path.GetFileName(file),
                Size = new FileInfo(file).Length,
                Data = Convert.ToBase64String(await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false))
            });
        }

        return new { SessionId = sessionId, Logs = logFiles };
    }

    public async Task DeleteLogsAsync(string sessionId, CancellationToken cancellationToken)
    {
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        var files = Directory.GetFiles(_baseDirectory, $"behavior_{sessionId}_*.bin.gz");
        foreach (var file in files)
        {
            File.Delete(file);
            _logDeletedBehaviorLog(_logger, Path.GetFileName(file), null);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disable();
        _flushTimer.Dispose();
        _flushLock.Dispose();
        _uploadTimer?.Dispose();
    }
}
