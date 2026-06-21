using Chronos.Core.Abstractions.Shared;
using System.Collections;
using System.Runtime.InteropServices;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public sealed class BinaryDataMapperTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }

        GC.SuppressFinalize(this);
    }

    private const uint ValidMagic = 0x53524843;
    private const int ValidVersion = 1;

    // ── WriteTicksToBinary ───────────────────────────────────────

    [Fact]
    public void WriteTicksToBinary_Creates_File_And_Roundtrips()
    {
        var ticks = new Tick[]
        {
            new(1, 1.0, 1.1, 0),
            new(2, 2.0, 2.1, 10, true),
            new(1000, 0.5, 0.6, 0)
        };

        var rawBytes = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, rawBytes);

        using var mmList = new MemoryMappedTickList(_tempFile);
        Assert.Equal(3, mmList.Count);
        for (int i = 0; i < ticks.Length; i++)
        {
            Assert.Equal(ticks[i].Time, mmList[i].Time);
            Assert.Equal(ticks[i].Bid, mmList[i].Bid, 10);
            Assert.Equal(ticks[i].Ask, mmList[i].Ask, 10);
            Assert.Equal(ticks[i].Volume, mmList[i].Volume, 10);
            Assert.Equal(ticks[i].IsSynthetic, mmList[i].IsSynthetic);
        }
    }

    [Fact]
    public void WriteTicksToBinary_Empty_Array_Produces_File_With_Zero_Ticks()
    {
        BinaryDataMapper.WriteTicksToBinary(_tempFile, Array.Empty<Tick>());
        using var mmList = BinaryDataMapper.MapTicks(_tempFile);
        Assert.Empty(mmList);
    }

    [Fact]
    public void WriteTicksToBinary_Null_Array_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BinaryDataMapper.WriteTicksToBinary(_tempFile, null!));
    }

    [Fact]
    public void WriteTicksToBinary_Unsorted_Ticks_Throws_AdapterException()
    {
        var ticks = new Tick[]
        {
            new(200, 1, 1, 1),
            new(100, 2, 2, 2)
        };

        Assert.Throws<AdapterException>(() => BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks));
    }

    [Fact]
    public void WriteTicksToBinary_Writes_Valid_Magic_And_Version()
    {
        var ticks = new Tick[] { new(1, 1, 1, 1) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        var fileBytes = File.ReadAllBytes(_tempFile);
        Assert.True(fileBytes.Length >= 8);
        var magic = BitConverter.ToUInt32(fileBytes, 0);
        var version = BitConverter.ToInt32(fileBytes, 4);
        Assert.Equal(ValidMagic, magic);
        Assert.Equal(ValidVersion, version);
    }

    // ── MapTicks ─────────────────────────────────────────────────

    [Fact]
    public void MapTicks_NonExistent_File_Returns_Empty_List()
    {
        var mmList = BinaryDataMapper.MapTicks("non_existent_file.chrs");
        Assert.Empty(mmList);
    }

    [Fact]
    public void MapTicks_File_With_Wrong_Magic_Still_Reads_As_Raw_Ticks()
    {
        var ticks = new Tick[] { new(5, 1, 2, 3) };
        var raw = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, raw);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
    }

    [Fact]
    public void MapTicks_File_Version_Mismatch_Reads_As_Raw_Ticks()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, 0x53524843u);
        BitConverter.TryWriteBytes(header[4..], 99);
        var ticks = new Tick[] { new(7, 8, 9, 10) };
        var body = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, header.ToArray().Concat(body).ToArray());
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.True(mm.Count > 0);
    }
}

public sealed class MemoryMappedTickListTests : IDisposable
{
    private readonly string _file = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }

        GC.SuppressFinalize(this);
    }

    // ── Construction ─────────────────────────────────────────────

    [Fact]
    public void Empty_File_Returns_Count_Zero()
    {
        using var mm = new MemoryMappedTickList(_file);
        Assert.Empty(mm);
    }

    [Fact]
    public void File_With_Header_Only_Count_Zero()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, 0x53524843u);
        BitConverter.TryWriteBytes(header[4..], 1);
        File.WriteAllBytes(_file, header.ToArray());
        using var mm = new MemoryMappedTickList(_file);
        Assert.Empty(mm);
    }

    [Fact]
    public void No_Header_File_Reads_All_Ticks()
    {
        var ticks = new Tick[] { new(1, 2, 3, 4) };
        var raw = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_file, raw);
        using var mm = new MemoryMappedTickList(_file);
        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
    }

    [Fact]
    public void NonExistent_File_Returns_Count_Zero()
    {
        using var mm = new MemoryMappedTickList("nonexistent.chrs");
        Assert.Empty(mm);
    }

    [Fact]
    public void File_With_Only_Small_Header_Returns_Count_Zero()
    {
        File.WriteAllBytes(_file, new byte[] { 0x43, 0x48, 0x52, 0x53, 0x01, 0x00, 0x00, 0x00, 0x01 });
        using var mm = new MemoryMappedTickList(_file);
        Assert.Empty(mm);
    }

    [Fact]
    public void File_Exactly_Header_Plus_One_Tick_Reads_Correctly()
    {
        var tick = new Tick(42, 10, 12, 5, true);
        BinaryDataMapper.WriteTicksToBinary(_file, new[] { tick });
        var info = new FileInfo(_file);
        Assert.Equal(8 + 33, info.Length);
        using var mm = new MemoryMappedTickList(_file);
        Assert.Single(mm);
        Assert.Equal(tick.Time, mm[0].Time);
        Assert.Equal(tick.Bid, mm[0].Bid);
        Assert.Equal(tick.Ask, mm[0].Ask);
        Assert.Equal(tick.Volume, mm[0].Volume);
        Assert.Equal(tick.IsSynthetic, mm[0].IsSynthetic);
    }

    [Fact]
    public void File_With_Trailing_Bytes_Reads_Whole_Ticks()
    {
        var ticks = new Tick[] { new(1, 2, 3, 4) };
        BinaryDataMapper.WriteTicksToBinary(_file, ticks);
        using (var fs = new FileStream(_file, FileMode.Append, FileAccess.Write))
        {
            fs.WriteByte(0xFF);
        }

        using var mm = BinaryDataMapper.MapTicks(_file);
        Assert.Single(mm);
    }

    // ── Indexing ─────────────────────────────────────────────────

    [Fact]
    public void Index_Out_Of_Range_Throws()
    {
        var ticks = new Tick[] { new(1, 2, 3, 4) };
        BinaryDataMapper.WriteTicksToBinary(_file, ticks);
        using var mm = BinaryDataMapper.MapTicks(_file);
        Assert.Throws<ArgumentOutOfRangeException>(() => mm[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => mm[-1]);
    }

    // ── Enumeration ──────────────────────────────────────────────

    [Fact]
    public void Enumeration_Works()
    {
        var ticks = new Tick[] { new(1, 2, 3, 4), new(5, 6, 7, 8) };
        var rawBytes = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_file, rawBytes);
        using var mm = new MemoryMappedTickList(_file);
        int count = 0;
        foreach (var t in mm)
        {
            Assert.Equal(ticks[count].Time, t.Time);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public void NonGeneric_Enumerator_Works()
    {
        var ticks = new Tick[] { new(1, 2, 3, 4), new(5, 6, 7, 8) };
        BinaryDataMapper.WriteTicksToBinary(_file, ticks);
        using var mm = new MemoryMappedTickList(_file);
        IEnumerable enumerable = mm;
        int count = 0;
        foreach (object? item in enumerable)
        {
            Assert.IsType<Tick>(item);
            count++;
        }
        Assert.Equal(2, count);
    }

    // ── Dispose ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        using var mm = new MemoryMappedTickList(_file);
        mm.Dispose();
        mm.Dispose();
    }

    [Fact]
    public void Index_Zero_Count_Returns_Throws()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using var mm = new MemoryMappedTickList(tempFile);
            Assert.Throws<ArgumentOutOfRangeException>(() => mm[0]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Dispose_On_Null_File_Does_Not_Throw()
    {
        using var mm = new MemoryMappedTickList("nonexistent_file_xyz.chrs");
        mm.Dispose();
        mm.Dispose();
    }
}

public sealed class MemoryMappedTickListHeaderTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }

        GC.SuppressFinalize(this);
    }

    private const uint ValidMagic = 0x53524843;
    private const int ValidVersion = 1;

    [Fact]
    public void MemoryMappedTickList_Skips_Header_When_Valid()
    {
        Tick[] ticks = [new(42, 10, 12, 5, true)];
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
    }

    [Fact]
    public void MemoryMappedTickList_Wrong_Version_Reads_From_Offset_Zero()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, ValidMagic);
        BitConverter.TryWriteBytes(header[4..], 99);
        var ticks = new Tick[] { new(7, 8, 9, 10) };
        var body = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, header.ToArray().Concat(body).ToArray());
        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.True(mm.Count > 0);
        Assert.NotEqual(7, mm[0].Time);
    }

    [Fact]
    public void MemoryMappedTickList_Invalid_Magic_Reads_From_Offset_Zero()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, 0xDEADBEEFu);
        BitConverter.TryWriteBytes(header[4..], ValidVersion);
        var ticks = new Tick[] { new(9, 10, 11, 12) };
        var body = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, header.ToArray().Concat(body).ToArray());
        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.True(mm.Count > 0);
        Assert.NotEqual(9, mm[0].Time);
    }

    [Fact]
    public void MemoryMappedTickList_File_Too_Small_Returns_Count_Zero()
    {
        File.WriteAllBytes(_tempFile, new byte[] { 1, 2, 3, 4 });
        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.Empty(mm);
    }
}
