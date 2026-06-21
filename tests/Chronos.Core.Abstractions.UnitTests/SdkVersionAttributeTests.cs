namespace Chronos.Core.Abstractions.UnitTests;

public class SdkVersionAttributeTests
{
    [Fact]
    public void Version_Stores_Value()
    {
        var attr = new SdkVersionAttribute("1.0.0");
        Assert.Equal("1.0.0", attr.Version);
    }

    [Fact]
    public void Attribute_Usage_Assembly_Only()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(SdkVersionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];
        Assert.Equal(AttributeTargets.Assembly, attrUsage.ValidOn);
        Assert.False(attrUsage.AllowMultiple);
    }
}
