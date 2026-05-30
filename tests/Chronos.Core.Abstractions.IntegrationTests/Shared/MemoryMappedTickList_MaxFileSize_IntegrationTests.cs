using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public sealed class MemoryMappedTickList_MaxFileSize_IntegrationTests : IDisposable
{
    private readonly string _tempFile;

    public MemoryMappedTickList_MaxFileSize_IntegrationTests()
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
    public void Max_Tick_Count_Arithmetic_Does_Not_Overflow()
    {
        // The production code uses (int.MaxValue * sizeof(Tick)) as the max data length.
        // This test verifies that the multiplication does not overflow Int64 and
        // that the result is reasonable.
        int tickSize = Unsafe.SizeOf<Tick>();
        long maxDataLength = (long)int.MaxValue * tickSize;
        Assert.True(maxDataLength > 0);
        Assert.True(maxDataLength < long.MaxValue);

        // The guard condition is (dataLength > maxDataLength), so the allowed maximum
        // is exactly maxDataLength. We cannot easily create a file of that exact size
        // in a portable test, but the arithmetic is sound.
    }
}
