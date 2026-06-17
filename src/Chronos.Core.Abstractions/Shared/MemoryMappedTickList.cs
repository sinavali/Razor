using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Provides read‑only access to a tick file using memory‑mapped I/O.
/// Implements <see cref="IDisposable"/> to release the memory‑mapped view.
/// </summary>
public sealed class MemoryMappedTickList : IReadOnlyList<Tick>, IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly int _count;
    private unsafe byte* _basePointer;
    private bool _disposed;

    /// <summary>
    /// Opens a binary tick file and maps it into virtual memory for zero‑copy read‑only access.
    /// The entire file is mapped; if a valid Chronos header is detected (magic <c>"CHRS"</c>, version <c>1</c>),
    /// the 8‑byte header is automatically skipped via pointer arithmetic so the returned
    /// <see cref="IReadOnlyList{T}"/> exposes only the tick payload.  Virtual address consumption
    /// equals the full file size, but physical memory is paged in on demand – safe for very large
    /// files (e.g. multi‑year tick archives).  Call <see cref="Dispose()"/> to release the mapping.
    /// </summary>
    /// <param name="filePath">Full path to the binary tick file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The file contains more than <c>2,147,483,647</c> ticks, which exceeds the maximum supported count.
    /// </exception>
    public unsafe MemoryMappedTickList(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            _count = 0;
            _mmf = null!;
            _accessor = null!;
            _basePointer = null;
            return;
        }

        int tickSize = Unsafe.SizeOf<Tick>();
        long fileLength = fileInfo.Length;

        long dataOffset = 0;

        using (var headerStream = new FileStream(
                   filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                   4096, FileOptions.SequentialScan))
        {
            if (fileLength >= BinaryDataMapper.HeaderSize)
            {
                Span<byte> header = stackalloc byte[BinaryDataMapper.HeaderSize];
                headerStream.ReadExactly(header);
                uint magic = BitConverter.ToUInt32(header);
                int version = BitConverter.ToInt32(header[4..]);
                if (magic == BinaryDataMapper.FileMagic
                    && version == BinaryDataMapper.FileVersion)
                {
                    dataOffset = BinaryDataMapper.HeaderSize;
                }
            }
        }

        long dataLength = fileLength - dataOffset;

        const long maxTicks = int.MaxValue;
        long maxDataLength = maxTicks * tickSize;
        if (dataLength > maxDataLength)
        {
            throw new NotSupportedException(
                $"File contains more than {maxTicks:N0} ticks, which exceeds the maximum supported per file.");
        }

        _count = (int)(dataLength / tickSize);

        if (_count == 0)
        {
            _mmf = null!;
            _accessor = null!;
            _basePointer = null;
            return;
        }

        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);

        _accessor = _mmf.CreateViewAccessor(0, fileLength,
            MemoryMappedFileAccess.Read);

        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        _basePointer = ptr + dataOffset;
    }

    /// <inheritdoc/>
    public int Count => _count;

    /// <inheritdoc/>
    public unsafe Tick this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Unsafe.Read<Tick>(_basePointer + index * Unsafe.SizeOf<Tick>());
        }
    }

    /// <inheritdoc/>
    public IEnumerator<Tick> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Releases the memory‑mapped view.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to release unmanaged resources if <see cref="Dispose()"/> was not called.
    /// </summary>
    /// <remarks>
    /// Only the raw pointer is released here; the <see cref="_accessor"/> and <see cref="_mmf"/>
    /// objects are allowed to finalize naturally because they hold their own managed handles.
    /// </remarks>
    ~MemoryMappedTickList() => Dispose(false);

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_accessor != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        if (disposing)
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }
}
