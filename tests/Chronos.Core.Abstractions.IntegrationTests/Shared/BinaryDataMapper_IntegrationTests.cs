using System.IO;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

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
        // Arrange: generate 100,000 ticks
        var ticks = new Tick[100_000];
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i] = new Tick(i * 10_000_000L, 100 + i * 0.01, 100.1 + i * 0.01, i % 10, i % 2 == 0);
        }

        // Act: write with header, then read back
        BinaryDataMapper.WriteTicksToBinary(_tempFile, ticks);
        using var mm = BinaryDataMapper.MapTicks(_tempFile);

        // Assert
        Assert.Equal(100_000, mm.Count);
        // Check first and last
        Assert.Equal(ticks[0].Time, mm[0].Time);
        Assert.Equal(ticks[^1].Time, mm[^1].Time);

        // Verify a sample in the middle
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
                break; // only enumerate 100 out of 5000
            }
        }
    }
}
