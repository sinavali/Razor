using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class ChronosRandomAdditionalMethodsTests
{
    [Fact]
    public void NextDouble_MaxValue_In_Range()
    {
        var rng = new ChronosRandom(42);
        for (int i = 0; i < 100; i++)
        {
            double d = rng.NextDouble(5.0);
            Assert.InRange(d, 0.0, 5.0 - double.Epsilon);
        }
    }

    [Fact]
    public void NextDouble_MinMax_In_Range()
    {
        var rng = new ChronosRandom(42);
        for (int i = 0; i < 100; i++)
        {
            double d = rng.NextDouble(10.0, 20.0);
            Assert.InRange(d, 10.0, 20.0 - double.Epsilon);
        }
    }

    [Fact]
    public void NextBytes_Null_Buffer_Throws()
    {
        var rng = new ChronosRandom(42);
        Assert.Throws<ArgumentNullException>(() => rng.NextBytes(null!));
    }
}
