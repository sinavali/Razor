using System.Runtime.InteropServices;

namespace Chronos.Abstractions.Shared;

/// <summary>
/// File‑I/O and memory‑mapped access utilities for tick data.
/// </summary>
public static class BinaryDataMapper
{
    internal const uint FileMagic = 0x53524843; // "CHRS"
    internal const int FileVersion = 1;
    internal const int HeaderSize = 8;

    /// <summary>Maps a binary tick file directly into a memory‑mapped list.</summary>
    public static MemoryMappedTickList MapTicks(string filePath) => new(filePath);

    /// <summary>Writes an array of ticks to a versioned binary file.</summary>
    public static void WriteTicksToBinary(string filePath, Tick[] ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        Span<byte> header = stackalloc byte[HeaderSize];
        BitConverter.TryWriteBytes(header, FileMagic);
        BitConverter.TryWriteBytes(header[4..], FileVersion);
        fs.Write(header);
        var byteSpan = MemoryMarshal.AsBytes(ticks.AsSpan());
        fs.Write(byteSpan);
    }
}