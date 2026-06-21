namespace Chronos.Core.Abstractions.IntegrationTests;

public class SdkVersionAttribute_IntegrationTests
{
    [Fact]
    public void Version_Stored_And_Retrieved()
    {
        var attr = new SdkVersionAttribute("2.1.5");
        Assert.Equal("2.1.5", attr.Version);
    }

    [Fact]
    public void Attribute_Usage_Verified_Via_Reflection()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(SdkVersionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];

        Assert.Equal(AttributeTargets.Assembly, attrUsage.ValidOn);
        Assert.False(attrUsage.AllowMultiple);
    }
}
