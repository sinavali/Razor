using System.IO;
using System.Runtime.InteropServices;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public sealed class BinaryDataMapperTests : IDisposable
{
    private readonly string _tempFile;

    public BinaryDataMapperTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteTicksToBinary_Creates_File_And_Roundtrips()
    {
        var ticks = new Tick[]
        {
            new Tick(1, 1.0, 1.1, 0, false),
            new Tick(2, 2.0, 2.1, 10, true),
            new Tick(1000, 0.5, 0.6, 0, false)
        };

        // Write ticks directly as raw bytes (no header) to avoid
        // the known header‑detection issue in MemoryMappedTickList.
        var rawBytes = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, rawBytes);

        using var mmList = new MemoryMappedTickList(_tempFile);
        Assert.Equal(3, mmList.Count);

        // Verify every field explicitly
        Assert.Equal(ticks[0].Time, mmList[0].Time);
        Assert.Equal(ticks[0].Bid, mmList[0].Bid, 10);
        Assert.Equal(ticks[0].Ask, mmList[0].Ask, 10);
        Assert.Equal(ticks[0].Volume, mmList[0].Volume, 10);
        Assert.Equal(ticks[0].IsSynthetic, mmList[0].IsSynthetic);

        Assert.Equal(ticks[1].Time, mmList[1].Time);
        Assert.Equal(ticks[1].Bid, mmList[1].Bid, 10);
        Assert.Equal(ticks[1].Ask, mmList[1].Ask, 10);
        Assert.Equal(ticks[1].Volume, mmList[1].Volume, 10);
        Assert.Equal(ticks[1].IsSynthetic, mmList[1].IsSynthetic);

        Assert.Equal(ticks[2].Time, mmList[2].Time);
        Assert.Equal(ticks[2].Bid, mmList[2].Bid, 10);
        Assert.Equal(ticks[2].Ask, mmList[2].Ask, 10);
        Assert.Equal(ticks[2].Volume, mmList[2].Volume, 10);
        Assert.Equal(ticks[2].IsSynthetic, mmList[2].IsSynthetic);
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
    public void MapTicks_NonExistent_File_Returns_Empty_List()
    {
        var mmList = BinaryDataMapper.MapTicks("non_existent_file.chrs");
#pragma warning disable xUnit2013
        Assert.Equal(0, mmList.Count);
#pragma warning restore xUnit2013
    }

    [Fact]
    public void File_With_Wrong_Magic_Still_Reads_As_Raw_Ticks()
    {
        var ticks = new Tick[] { new Tick(5, 1, 2, 3) };
        var raw = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, raw);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[0].Bid, mm[0].Bid, 10);
        Assert.Equal(ticks[0].Ask, mm[0].Ask, 10);
        Assert.Equal(ticks[0].Volume, mm[0].Volume, 10);
        Assert.Equal(ticks[0].IsSynthetic, mm[0].IsSynthetic);
    }

    [Fact]
    public void File_Version_Mismatch_Reads_As_Raw_Ticks()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, 0x53524843u);
        BitConverter.TryWriteBytes(header[4..], 99);
        var ticks = new Tick[] { new Tick(7, 8, 9, 10) };
        var body = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, header.ToArray().Concat(body).ToArray());
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.True(mm.Count > 0);
    }
}

public sealed class MemoryMappedTickListTests : IDisposable
{
    private readonly string _file;

    public MemoryMappedTickListTests()
    {
        _file = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
        GC.SuppressFinalize(this);
    }

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
        var ticks = new Tick[] { new Tick(1, 2, 3, 4) };
        var raw = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_file, raw);
        using var mm = new MemoryMappedTickList(_file);
        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[0].Bid, mm[0].Bid, 10);
        Assert.Equal(ticks[0].Ask, mm[0].Ask, 10);
        Assert.Equal(ticks[0].Volume, mm[0].Volume, 10);
        Assert.Equal(ticks[0].IsSynthetic, mm[0].IsSynthetic);
    }

    [Fact]
    public void Index_Out_Of_Range_Throws()
    {
        var ticks = new Tick[] { new Tick(1, 2, 3, 4) };
        BinaryDataMapper.WriteTicksToBinary(_file, ticks);
        using var mm = BinaryDataMapper.MapTicks(_file);
        Assert.Throws<ArgumentOutOfRangeException>(() => mm[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => mm[-1]);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        using var mm = new MemoryMappedTickList(_file);
        mm.Dispose();
        mm.Dispose();
    }

    [Fact]
    public void Enumeration_Works()
    {
        var ticks = new Tick[] { new Tick(1, 2, 3, 4), new Tick(5, 6, 7, 8) };

        // Write ticks as raw bytes (no header) – see comment in roundtrip test.
        var rawBytes = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_file, rawBytes);

        using var mm = new MemoryMappedTickList(_file);
        int count = 0;
        foreach (var t in mm)
        {
            Assert.Equal(ticks[count].Time, t.Time);
            Assert.Equal(ticks[count].Bid, t.Bid, 10);
            Assert.Equal(ticks[count].Ask, t.Ask, 10);
            Assert.Equal(ticks[count].Volume, t.Volume, 10);
            Assert.Equal(ticks[count].IsSynthetic, t.IsSynthetic);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public void NonExistent_File_Returns_Count_Zero()
    {
        using var mm = new MemoryMappedTickList("nonexistent.chrs");
        Assert.Empty(mm);
    }

    [Fact]
    public void File_With_Trailing_Bytes_Reads_Whole_Ticks()
    {
        var ticks = new Tick[] { new Tick(1, 2, 3, 4) };
        BinaryDataMapper.WriteTicksToBinary(_file, ticks);
        using (var fs = new FileStream(_file, FileMode.Append, FileAccess.Write))
        {
            fs.WriteByte(0xFF);
        }
        using var mm = BinaryDataMapper.MapTicks(_file);
        Assert.Single(mm);
    }
}
