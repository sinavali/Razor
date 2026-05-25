using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class BarEdgeCaseTests
{
    [Fact]
    public void Equals_With_Non_Bar_Returns_False()
    {
        var bar = new Bar(1, 2, 3, 4, 5, 6, 7);
        Assert.False(bar.Equals("not a bar"));
    }
}
