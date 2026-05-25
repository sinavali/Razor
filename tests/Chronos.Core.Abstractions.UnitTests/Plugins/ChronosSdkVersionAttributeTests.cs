using Chronos.Core.Abstractions.Plugins;

namespace Chronos.Core.Abstractions.UnitTests.Plugins;

public class ChronosSdkVersionAttributeTests
{
    [Fact]
    public void Version_Stores_Value()
    {
        var attr = new ChronosSdkVersionAttribute("1.0.0");
        Assert.Equal("1.0.0", attr.Version);
    }

    [Fact]
    public void Attribute_Usage_Assembly_Only()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(ChronosSdkVersionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];
        Assert.Equal(AttributeTargets.Assembly, attrUsage.ValidOn);
        Assert.False(attrUsage.AllowMultiple);
    }
}
