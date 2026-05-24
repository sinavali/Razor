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
    private readonly FileStream _fileStream;
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly int _count;
    private readonly bool _ownsStream;
    private unsafe byte* _basePointer;
    private bool _disposed;

    /// <summary>
    /// Opens a binary tick file and maps it into virtual memory.
    /// </summary>
    public unsafe MemoryMappedTickList(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            _count = 0;
            _fileStream = null!;
            _mmf = null!;
            _accessor = null!;
            _basePointer = null;
            return;
        }

        int tickSize = Unsafe.SizeOf<Tick>();
        long fileLength = fileInfo.Length;

        long dataOffset = 0;
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        _ownsStream = true;

        if (fileLength >= BinaryDataMapper.HeaderSize)
        {
            Span<byte> header = stackalloc byte[BinaryDataMapper.HeaderSize];
            _fileStream.ReadExactly(header);
            uint magic = BitConverter.ToUInt32(header);
            int version = BitConverter.ToInt32(header[4..]);
            if (magic == BinaryDataMapper.FileMagic && version == BinaryDataMapper.FileVersion)
            {
                dataOffset = BinaryDataMapper.HeaderSize;
            }
            else
            {
                _fileStream.Seek(0, SeekOrigin.Begin);
            }
        }

        long dataLength = fileLength - dataOffset;

        const long maxTicks = int.MaxValue;
        long maxDataLength = maxTicks * tickSize;
        if (dataLength > maxDataLength)
            throw new NotSupportedException(
                $"File contains more than {maxTicks:N0} ticks, which exceeds the maximum supported per file.");

        _count = (int)(dataLength / tickSize);

        if (_count == 0)
        {
            _fileStream.Dispose();
            _mmf = null!;
            _accessor = null!;
            _basePointer = null;
            return;
        }

        _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
        _accessor = _mmf.CreateViewAccessor(dataOffset, dataLength, MemoryMappedFileAccess.Read);

        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _basePointer = ptr;
    }

    /// <inheritdoc/>
    public int Count => _count;

    /// <inheritdoc/>
    public unsafe Tick this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return Unsafe.Read<Tick>(_basePointer + index * Unsafe.SizeOf<Tick>());
        }
    }

    /// <inheritdoc/>
    public IEnumerator<Tick> GetEnumerator()
    {
        for (int i = 0; i < _count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Releases the memory‑mapped view and file stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_accessor != null)
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();

        _accessor?.Dispose();
        _mmf?.Dispose();
        if (_ownsStream)
            _fileStream?.Dispose();
    }
}
