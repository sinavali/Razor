// -----------------------------------------------------------------------------
// <copyright file="BinaryTransferManager.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Communication;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

/// <summary>Manages state for ongoing binary transfers.</summary>
internal sealed class BinaryTransferManager : IDisposable
{
    private readonly ILogger<BinaryTransferManager> _logger;
    private readonly ConcurrentDictionary<string, IncomingTransfer> _incoming = new();
    private readonly ConcurrentDictionary<string, OutgoingTransfer> _outgoing = new();
    private bool _disposed;

    // Logger delegates
    private static readonly Action<ILogger, string, string, long, Exception?> _logTransferStarted =
        LoggerMessage.Define<string, string, long>(LogLevel.Information, 0,
            "Binary transfer started: {TransferId}, File: {FileName}, Size: {TotalSize}");

    private static readonly Action<ILogger, string, int, long, Exception?> _logChunkReceived =
        LoggerMessage.Define<string, int, long>(LogLevel.Debug, 1,
            "Binary chunk received: {TransferId}, Offset: {Offset}, Size: {Size}");

    private static readonly Action<ILogger, string, Exception?> _logTransferCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 2,
            "Binary transfer completed: {TransferId}");

    private static readonly Action<ILogger, string, Exception?> _logTransferFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 3,
            "Binary transfer failed: {TransferId}");

    private static readonly Action<ILogger, string, Exception?> _logTransferCancelled =
        LoggerMessage.Define<string>(LogLevel.Warning, 4,
            "Binary transfer cancelled: {TransferId}");

    private static readonly Action<ILogger, string, long, long, Exception?> _logOutOfOrderChunk =
        LoggerMessage.Define<string, long, long>(LogLevel.Warning, 6,
            "Out-of-order chunk for {TransferId}: expected offset {Expected}, got {Actual}");

    private static readonly Action<ILogger, string, long, Exception?> _logRetransmitRequest =
        LoggerMessage.Define<string, long>(LogLevel.Information, 7,
            "Retransmit requested for {TransferId} at offset {ExpectedOffset}");

    public BinaryTransferManager(ILogger<BinaryTransferManager> logger)
    {
        _logger = logger;
    }

    /// <summary>Starts a new incoming binary transfer.</summary>
    public void StartIncoming(string transferId, string fileName, long totalSize, string checksum, string contentType)
    {
        if (_incoming.ContainsKey(transferId))
        {
            throw new InvalidOperationException($"Transfer {transferId} already exists.");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"chronos_incoming_{transferId}.tmp");
        var transfer = new IncomingTransfer
        {
            TransferId = transferId,
            FileName = fileName,
            TotalSize = totalSize,
            Checksum = checksum,
            ContentType = contentType,
            TempFilePath = tempPath,
            FileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan),
            ReceivedBytes = 0,
            Completed = false,
            Cancelled = false
        };

        _incoming[transferId] = transfer;
        _logTransferStarted(_logger, transferId, fileName, totalSize, null);
    }

    /// <summary>Appends a chunk to an incoming transfer.</summary>
    /// <returns>True if the transfer is now complete; otherwise false.</returns>
    public bool AppendChunk(string transferId, long offset, byte[] data)
    {
        if (!_incoming.TryGetValue(transferId, out var transfer))
        {
            throw new InvalidOperationException($"Transfer {transferId} not found.");
        }

        if (transfer.Completed || transfer.Cancelled)
        {
            return false;
        }

        // Validate offset
        if (offset != transfer.ReceivedBytes)
        {
            _logOutOfOrderChunk(_logger, transferId, transfer.ReceivedBytes, offset, null);
            return false;
        }

        // Ensure file stream is not null
        if (transfer.FileStream == null)
        {
            throw new InvalidOperationException($"FileStream for transfer {transferId} is null.");
        }

        // Write chunk to disk
        transfer.FileStream.Write(data, 0, data.Length);
        transfer.ReceivedBytes += data.Length;
        _logChunkReceived(_logger, transferId, data.Length, offset, null);

        if (transfer.ReceivedBytes == transfer.TotalSize)
        {
            transfer.FileStream.Flush();
            transfer.FileStream.Close();
            transfer.FileStream.Dispose();

            // Verify checksum
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(transfer.TempFilePath);
            var computed = Convert.ToHexString(sha.ComputeHash(fs));
            if (!computed.Equals(transfer.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                _logTransferFailed(_logger, transferId, null);
                transfer.Completed = false;
                transfer.Cancelled = true;
                File.Delete(transfer.TempFilePath);
                throw new InvalidOperationException($"Checksum mismatch for {transferId}: expected {transfer.Checksum}, got {computed}");
            }

            transfer.Completed = true;
            _logTransferCompleted(_logger, transferId, null);
            return true;
        }

        return false;
    }

    /// <summary>Completes an incoming transfer (called after End message).</summary>
    public IncomingTransfer? GetCompletedTransfer(string transferId)
    {
        if (_incoming.TryRemove(transferId, out var transfer) && transfer.Completed)
        {
            return transfer;
        }
        return null;
    }

    /// <summary>Cancels an incoming transfer.</summary>
    public void CancelIncoming(string transferId)
    {
        if (_incoming.TryRemove(transferId, out var transfer))
        {
            transfer.Cancelled = true;
            transfer.FileStream?.Dispose();
            if (File.Exists(transfer.TempFilePath))
            {
                File.Delete(transfer.TempFilePath);
            }
            _logTransferCancelled(_logger, transferId, null);
        }
    }

    /// <summary>Registers an outgoing transfer (for sending).</summary>
    public void StartOutgoing(string transferId, string filePath, string contentType, long totalSize, string checksum)
    {
        if (_outgoing.ContainsKey(transferId))
        {
            throw new InvalidOperationException($"Transfer {transferId} already exists.");
        }

        var transfer = new OutgoingTransfer
        {
            TransferId = transferId,
            FilePath = filePath,
            ContentType = contentType,
            TotalSize = totalSize,
            Checksum = checksum,
            SentBytes = 0,
            Completed = false,
            Cancelled = false
        };

        _outgoing[transferId] = transfer;
        _logTransferStarted(_logger, transferId, Path.GetFileName(filePath), totalSize, null);
    }

    /// <summary>Gets the next chunk to send for an outgoing transfer.</summary>
    public (long offset, byte[]? data) GetNextChunk(string transferId, int chunkSize)
    {
        if (!_outgoing.TryGetValue(transferId, out var transfer))
        {
            throw new InvalidOperationException($"Outgoing transfer {transferId} not found.");
        }

        if (transfer.Completed || transfer.Cancelled)
        {
            return (0, null);
        }

        long remaining = transfer.TotalSize - transfer.SentBytes;
        if (remaining <= 0)
        {
            transfer.Completed = true;
            return (0, null);
        }

        int size = (int)Math.Min(chunkSize, remaining);
        byte[] data = new byte[size];

        if (transfer.FileStream == null)
        {
            transfer.FileStream = new FileStream(transfer.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
        }

        transfer.FileStream.Seek(transfer.SentBytes, SeekOrigin.Begin);
        int bytesRead = transfer.FileStream.Read(data, 0, size);
        if (bytesRead < size)
        {
            // End of file
            transfer.Completed = true;
            return (0, null);
        }

        long offset = transfer.SentBytes;
        transfer.SentBytes += bytesRead;
        return (offset, data);
    }

    /// <summary>Handles a retransmit request from the receiver.</summary>
    /// <param name="transferId">The transfer ID.</param>
    /// <param name="expectedOffset">The offset from which the receiver wants to resume.</param>
    public void HandleRetransmitRequest(string transferId, long expectedOffset)
    {
        if (!_outgoing.TryGetValue(transferId, out var transfer))
        {
            _logTransferCancelled(_logger, transferId, null);
            return;
        }

        if (transfer.Completed || transfer.Cancelled)
        {
            return;
        }

        // Clamp to a valid range
        if (expectedOffset < 0)
        {
            expectedOffset = 0;
        }
        if (expectedOffset > transfer.TotalSize)
        {
            expectedOffset = transfer.TotalSize;
        }

        // Reset the sent position to the requested offset.
        // The next call to GetNextChunk will resume from there.
        transfer.SentBytes = expectedOffset;
        _logRetransmitRequest(_logger, transferId, expectedOffset, null);
    }

    /// <summary>Completes an outgoing transfer.</summary>
    public void CompleteOutgoing(string transferId)
    {
        if (_outgoing.TryRemove(transferId, out var transfer))
        {
            transfer.Completed = true;
            transfer.FileStream?.Dispose();
            _logTransferCompleted(_logger, transferId, null);
        }
    }

    /// <summary>Cancels an outgoing transfer.</summary>
    public void CancelOutgoing(string transferId)
    {
        if (_outgoing.TryRemove(transferId, out var transfer))
        {
            transfer.Cancelled = true;
            transfer.FileStream?.Dispose();
            _logTransferCancelled(_logger, transferId, null);
        }
    }

    /// <summary>Gets a snapshot of active transfers for debugging.</summary>
    public object GetStatus()
    {
        return new
        {
            Incoming = _incoming.Keys,
            Outgoing = _outgoing.Keys
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var kv in _incoming)
        {
            kv.Value.FileStream?.Dispose();
            if (File.Exists(kv.Value.TempFilePath))
            {
                try { File.Delete(kv.Value.TempFilePath); } catch { }
            }
        }
        _incoming.Clear();

        foreach (var kv in _outgoing)
        {
            kv.Value.FileStream?.Dispose();
        }
        _outgoing.Clear();
    }

    /// <summary>Represents an incoming transfer.</summary>
    internal sealed class IncomingTransfer
    {
        public string TransferId { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public long TotalSize { get; init; }
        public string Checksum { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public string TempFilePath { get; init; } = string.Empty;
        public FileStream? FileStream { get; set; }
        public long ReceivedBytes { get; set; }
        public bool Completed { get; set; }
        public bool Cancelled { get; set; }
    }

    /// <summary>Represents an outgoing transfer.</summary>
    internal sealed class OutgoingTransfer
    {
        public string TransferId { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long TotalSize { get; init; }
        public string Checksum { get; init; } = string.Empty;
        public long SentBytes { get; set; }
        public bool Completed { get; set; }
        public bool Cancelled { get; set; }
        public FileStream? FileStream { get; set; }
    }
}
