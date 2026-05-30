using System.IO;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public sealed class MemoryMappedTickListBoundaryTests : IDisposable
{
    private readonly string _tempFile;

    public MemoryMappedTickListBoundaryTests()
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
    public void File_Exactly_Header_Plus_One_Tick_Reads_Correctly()
    {
        // 8 header bytes + 33 tick bytes = 41 bytes total.
        var tick = new Tick(42, 10, 12, 5, true);
        BinaryDataMapper.WriteTicksToBinary(_tempFile, new[] { tick });

        // Verify file size
        var info = new FileInfo(_tempFile);
        Assert.Equal(8 + 33, info.Length);

        using var mm = new MemoryMappedTickList(_tempFile);
        Assert.Single(mm);
        Assert.Equal(tick.Time, mm[0].Time);
        Assert.Equal(tick.Bid, mm[0].Bid);
        Assert.Equal(tick.Ask, mm[0].Ask);
        Assert.Equal(tick.Volume, mm[0].Volume);
        Assert.Equal(tick.IsSynthetic, mm[0].IsSynthetic);
    }
}
