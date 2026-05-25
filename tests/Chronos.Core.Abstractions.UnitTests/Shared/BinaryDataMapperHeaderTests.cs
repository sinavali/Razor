using System.IO;
using System.Runtime.InteropServices;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public sealed class BinaryDataMapperHeaderTests : IDisposable
{
    private readonly string _tempFile;

    public BinaryDataMapperHeaderTests()
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

    private const uint ValidMagic = 0x53524843; // "CHRS"
    private const int ValidVersion = 1;

    [Fact]
    public void WriteTicksToBinary_Writes_Valid_Magic_And_Version()
    {
        var ticks = new Tick[] { new Tick(1, 1, 1, 1) };
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);

        var fileBytes = File.ReadAllBytes(_tempFile);
        Assert.True(fileBytes.Length >= 8);
        var magic = BitConverter.ToUInt32(fileBytes, 0);
        var version = BitConverter.ToInt32(fileBytes, 4);
        Assert.Equal(ValidMagic, magic);
        Assert.Equal(ValidVersion, version);
    }

    [Fact]
    public void MemoryMappedTickList_Skips_Header_When_Valid()
    {
        Tick[] ticks = [new Tick(42, 10, 12, 5, true)];
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);

        using var mm = new MemoryMappedTickList(_tempFile);

        Assert.Single(mm);
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[0].Bid, mm[0].Bid);
        Assert.Equal(ticks[0].Ask, mm[0].Ask);
        Assert.Equal(ticks[0].Volume, mm[0].Volume);
        Assert.Equal(ticks[0].IsSynthetic, mm[0].IsSynthetic);
    }

    [Fact]
    public void MemoryMappedTickList_Wrong_Version_Reads_From_Offset_Zero()
    {
        Span<byte> header = stackalloc byte[8];
        BitConverter.TryWriteBytes(header, ValidMagic);
        BitConverter.TryWriteBytes(header[4..], 99);
        var ticks = new Tick[] { new Tick(7, 8, 9, 10) };
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
        var ticks = new Tick[] { new Tick(9, 10, 11, 12) };
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
