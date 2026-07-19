using Razor.Core.Sdk.Shared;
using Razor.Core.Shared;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class ValidationHelpers_IntegrationTests
{
    [Fact]
    public void ValidateNotNull_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ValidationHelpers.ValidateNotNull<object>(null!, "obj"));
    }

    [Fact]
    public void ValidateNotNull_Passes_For_Valid_Object()
    {
        var obj = new object();
        ValidationHelpers.ValidateNotNull(obj, "obj");
    }

    [Fact]
    public void ValidateRange_Throws_OutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationHelpers.ValidateRange(-1.0, 0, 100, "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationHelpers.ValidateRange(101.0, 0, 100, "value"));
    }

    [Fact]
    public void ValidateRange_Accepts_InRange()
    {
        ValidationHelpers.ValidateRange(0, 0, 100, "value");
        ValidationHelpers.ValidateRange(50, 0, 100, "value");
        ValidationHelpers.ValidateRange(100, 0, 100, "value");
    }
}
