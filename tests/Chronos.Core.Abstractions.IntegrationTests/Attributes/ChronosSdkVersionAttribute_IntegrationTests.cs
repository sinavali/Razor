using Chronos.Core.Abstractions;

namespace Chronos.Core.Abstractions.IntegrationTests.Attributes;

public class ChronosSdkVersionAttribute_IntegrationTests
{
    [Fact]
    public void Version_Stored_And_Retrieved()
    {
        var attr = new ChronosSdkVersionAttribute("2.1.5");
        Assert.Equal("2.1.5", attr.Version);
    }

    [Fact]
    public void Attribute_Usage_Verified_Via_Reflection()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(ChronosSdkVersionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];

        Assert.Equal(AttributeTargets.Assembly, attrUsage.ValidOn);
        Assert.False(attrUsage.AllowMultiple);
    }
}
