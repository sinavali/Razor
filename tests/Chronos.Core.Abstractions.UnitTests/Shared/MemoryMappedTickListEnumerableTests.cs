using System.Collections;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class MemoryMappedTickListEnumerableTests
{
    [Fact]
    public void NonGeneric_Enumerator_Works()
    {
        var ticks = new Tick[] { new(1, 2, 3, 4), new(5, 6, 7, 8) };
        var tempFile = Path.GetTempFileName();
        try
        {
            BinaryDataMapper.WriteTicksToBinary(tempFile, ticks);
            using var mm = new MemoryMappedTickList(tempFile);
            IEnumerable enumerable = mm;
            int count = 0;
            foreach (object? item in enumerable)
            {
                Assert.IsType<Tick>(item);
                count++;
            }
            Assert.Equal(2, count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
