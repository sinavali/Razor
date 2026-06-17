using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Shared;

public class PositionCloseSequenceTests
{
    [Fact]
    public void CloseSequence_Default_Is_Zero()
    {
        var p = new Position();
        Assert.Equal(0, p.CloseSequence);
    }

    [Fact]
    public void CloseSequence_Can_Be_Set()
    {
        var p = new Position { CloseSequence = 3 };
        Assert.Equal(3, p.CloseSequence);
    }
}
