using System.Collections.Concurrent;
using System.IO.Compression;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Services.BehaviorRecorder;

internal interface IBehaviorRecorder
{
    void Enable(string sessionId, string strategyName, double[] genes);
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
    private readonly string _baseDirectory;
    private readonly ConcurrentQueue<BehaviorRecord> _buffer = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private bool _isEnabled;
    private string _sessionId = string.Empty;
    private string _strategyName = string.Empty;
    private double[] _genes = Array.Empty<double>();
    private int _recordCount;
    private readonly int _flushThreshold = 10000;
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logBehaviorLoggingEnabled =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Behavior logging enabled for session {SessionId}.");
    private static readonly Action<ILogger, Exception?> _logBehaviorLoggingDisabled =
        LoggerMessage.Define(LogLevel.Information, 1, "Behavior logging disabled.");
    private static readonly Action<ILogger, int, string, Exception?> _logFlushedRecords =
        LoggerMessage.Define<int, string>(LogLevel.Debug, 2, "Flushed {Count} records to {FilePath}.");
    private static readonly Action<ILogger, string, Exception?> _logDeletedBehaviorLog =
        LoggerMessage.Define<string>(LogLevel.Information, 3, "Deleted behavior log: {File}");

    public bool IsEnabled => _isEnabled;

    public BehaviorRecorder(ILogger<BehaviorRecorder> logger)
    {
        _logger = logger;
        _baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "behavior_logs");
        Directory.CreateDirectory(_baseDirectory);
        _flushTimer = new Timer(async _ => await FlushAsync(CancellationToken.None).ConfigureAwait(false), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void Enable(string sessionId, string strategyName, double[] genes)
    {
        _sessionId = sessionId;
        _strategyName = strategyName;
        _genes = genes ?? Array.Empty<double>();
        _isEnabled = true;
        _recordCount = 0;
        _logBehaviorLoggingEnabled(_logger, sessionId, null);
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
    }
}
