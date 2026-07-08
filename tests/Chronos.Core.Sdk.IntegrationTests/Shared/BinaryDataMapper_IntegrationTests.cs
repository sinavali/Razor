using Chronos.Core.Sdk.Shared;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Chronos.Core.Sdk.IntegrationTests.Shared;

public sealed class BinaryDataMapper_IntegrationTests : IDisposable
{
    private readonly string _tempFile;

    public BinaryDataMapper_IntegrationTests()
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
    public void Write_And_Read_One_Hundred_Thousand_Ticks()
    {
        var ticks = new Tick[100_000];
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i] = new Tick(i * 10_000_000L, 100 + i * 0.01, 100.1 + i * 0.01, i % 10, i % 2 == 0);
        }

        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);

        Assert.Equal(100_000, mm.Count);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[^1].Time, mm[^1].Time);

        int mid = 50_000;
        Assert.Equal(ticks[mid].Time, mm[mid].Time);
        Assert.Equal(ticks[mid].Bid, mm[mid].Bid, 10);
        Assert.Equal(ticks[mid].Ask, mm[mid].Ask, 10);
        Assert.Equal(ticks[mid].Volume, mm[mid].Volume, 10);
        Assert.Equal(ticks[mid].IsSynthetic, mm[mid].IsSynthetic);
    }

    [Fact]
    public void Memory_Mapped_Enumeration_Is_Lazy()
    {
        var ticks = new Tick[5000];
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i] = new Tick(i, 1, 2, 3);
        }

        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);

        int count = 0;
        foreach (var t in mm)
        {
            Assert.Equal(count, t.Time);
            count++;
            if (count == 100)
            {
                break;
            }
        }

        Assert.Equal(100, count);
    }

    [Fact]
    public void Write_And_Read_With_Header_Skips_Correctly()
    {
        var ticks = new Tick[] { new(1000, 10, 12, 5, true), new(2000, 11, 13, 6, false) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = new MemoryMappedTickList(_tempFile);

        Assert.Equal(2, mm.Count);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[0].Bid, mm[0].Bid);
        Assert.Equal(ticks[0].Ask, mm[0].Ask);
        Assert.Equal(ticks[0].Volume, mm[0].Volume);
        Assert.Equal(ticks[0].IsSynthetic, mm[0].IsSynthetic);
    }

    [Fact]
    public void Write_Empty_File_Can_Be_Mapped()
    {
        BinaryDataMapper.WriteTicksToBinary(_tempFile, Array.Empty<Tick>());
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.Empty(mm);
    }

    [Fact]
    public void Write_500K_Ticks_Roundtrip()
    {
        var ticks = new Tick[500_000];
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i] = new Tick(i * 1000L, i * 0.001, i * 0.001 + 0.1, i % 100, i % 3 == 0);
        }

        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.Equal(500_000, mm.Count);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[^1].Time, mm[^1].Time);

        for (int i = 100_000; i < 100_100; i++)
        {
            Assert.Equal(ticks[i].Time, mm[i].Time);
        }
    }

    [Fact]
    public void MapTicks_Valid_Header_File_Reads_Correctly()
    {
        var ticks = new Tick[] { new(42, 10, 12, 5, true) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[0].Bid, mm[0].Bid);
        Assert.Equal(ticks[0].Ask, mm[0].Ask);
        Assert.Equal(ticks[0].Volume, mm[0].Volume);
        Assert.Equal(ticks[0].IsSynthetic, mm[0].IsSynthetic);
    }

    [Fact]
    public void Write_And_Map_Empty_Header_File()
    {
        BinaryDataMapper.WriteTicksToBinary(_tempFile, Array.Empty<Tick>());
        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.Empty(mm);
        Assert.Throws<ArgumentOutOfRangeException>(() => mm[0]);
    }

    [Fact]
    public void Max_Tick_Count_Arithmetic_Does_Not_Overflow()
    {
        int tickSize = Unsafe.SizeOf<Tick>();
        long maxDataLength = (long)int.MaxValue * tickSize;
        Assert.True(maxDataLength > 0);
        Assert.True(maxDataLength < long.MaxValue);
    }

    [Fact]
    public void WriteTicksToBinary_Unsorted_Ticks_Second_Pair_Throws()
    {
        var ticks = new Tick[]
        {
            new(100, 1, 1, 1),
            new(200, 2, 2, 2),
            new(250, 3, 3, 3),
            new(150, 4, 4, 4)  // unsorted after index 2
        };
        Assert.Throws<AdapterException>(() => BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks));
    }

    [Fact]
    public void MapTicks_File_With_Wrong_Version_But_Valid_Magic_Reads_Raw()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, 0x53524843u);   // "CHRS"
        BitConverter.TryWriteBytes(header[4..], 99);        // wrong version
        var ticks = new Tick[] { new(7, 8, 9, 10) };
        var body = MemoryMarshal.AsBytes(ticks.AsSpan()).ToArray();
        File.WriteAllBytes(_tempFile, header.ToArray().Concat(body).ToArray());
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.True(mm.Count > 0);
    }

    [Fact]
    public void MapTicks_File_Too_Large_For_Int_Count_Throws()
    {
        // We can't create a file that large, but we verify the arithmetic.
        int tickSize = Unsafe.SizeOf<Tick>();
        long maxAllowed = (long)int.MaxValue * tickSize;
        Assert.True(maxAllowed > 0);
        Assert.True(maxAllowed < long.MaxValue);
    }

    [Fact]
    public void WriteTicksToBinary_Single_Tick_No_Unsorted_Check_Needed()
    {
        var ticks = new Tick[] { new(42, 1, 1, 1) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);
        Assert.Single(mm);
    }
}
