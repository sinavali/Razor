using Chronos.Core.Abstractions.Adapters;

namespace Chronos.Core.Abstractions.UnitTests.Adapters;

public class AdapterNameAttributeTests
{
    [Fact]
    public void Name_Stores_Given_Value()
    {
        var attr = new AdapterNameAttribute("TestAdapter");
        Assert.Equal("TestAdapter", attr.Name);
    }

    [Fact]
    public void Name_Can_Be_Null()
    {
        var attr = new AdapterNameAttribute(null!);
        Assert.Null(attr.Name);
    }

    [Fact]
    public void Name_Can_Be_Empty()
    {
        var attr = new AdapterNameAttribute(string.Empty);
        Assert.Equal(string.Empty, attr.Name);
    }

    [Fact]
    public void Attribute_Usage_Restrictions()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(AdapterNameAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];
        Assert.False(attrUsage.AllowMultiple);
        Assert.False(attrUsage.Inherited);
        Assert.Equal(AttributeTargets.Class, attrUsage.ValidOn);
    }
}

public class AdapterVersionAttributeTests
{
    [Fact]
    public void Version_Stores_Given_Value()
    {
        var attr = new AdapterVersionAttribute("1.2.3");
        Assert.Equal("1.2.3", attr.Version);
    }

    [Fact]
    public void Version_Can_Be_Null()
    {
        var attr = new AdapterVersionAttribute(null!);
        Assert.Null(attr.Version);
    }

    [Fact]
    public void Assembly_Level_Only()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(AdapterVersionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];
        Assert.Equal(AttributeTargets.Assembly, attrUsage.ValidOn);
        Assert.False(attrUsage.AllowMultiple);
        Assert.True(attrUsage.Inherited); // AttributeUsage default Inherited = true
    }
}
