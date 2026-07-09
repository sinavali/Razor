using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.IntegrationTests.Shared;

public class GeneAttribute_IntegrationTests
{
    [Fact]
    public void GeneAttribute_All_Properties_Roundtrip()
    {
        var attr = new GeneAttribute(0, 100, 5, GeneType.Discrete)
        {
            Name = "TestGene",
            Order = 10
        };

        Assert.Equal(0, attr.Min);
        Assert.Equal(100, attr.Max);
        Assert.Equal(5, attr.Step);
        Assert.Equal(GeneType.Discrete, attr.Type);
        Assert.Equal("TestGene", attr.Name);
        Assert.Equal(10, attr.Order);
    }

    [Fact]
    public void GeneAttribute_Default_Values()
    {
        var attr = new GeneAttribute(-1.0, 1.0);

        Assert.Equal(-1.0, attr.Min);
        Assert.Equal(1.0, attr.Max);
        Assert.Equal(0, attr.Step);
        Assert.Equal(GeneType.Continuous, attr.Type);
        Assert.Equal("", attr.Name);
        Assert.Equal(0, attr.Order);
    }

    [Fact]
    public void GeneAttribute_All_GeneTypes()
    {
        var continuous = new GeneAttribute(0, 1, 0, GeneType.Continuous);
        Assert.Equal(GeneType.Continuous, continuous.Type);
        Assert.Equal(0, continuous.Step);

        var discrete = new GeneAttribute(0, 100, 10, GeneType.Discrete);
        Assert.Equal(GeneType.Discrete, discrete.Type);
        Assert.Equal(10, discrete.Step);

        var categorical = new GeneAttribute(0, 5, 1, GeneType.Categorical);
        Assert.Equal(GeneType.Categorical, categorical.Type);

        var structural = new GeneAttribute(0, 100, 0, GeneType.Structural);
        Assert.Equal(GeneType.Structural, structural.Type);

        var parametric = new GeneAttribute(-1, 1, 0, GeneType.Parametric);
        Assert.Equal(GeneType.Parametric, parametric.Type);
    }

    [Fact]
    public void GeneAttribute_AttributeUsage_Restrictions()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(GeneAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), true)[0];

        Assert.Equal(AttributeTargets.Property, attrUsage.ValidOn);
        Assert.False(attrUsage.AllowMultiple);
        Assert.False(attrUsage.Inherited);
    }

    [Fact]
    public void GeneAttribute_Invalid_Step_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, -1, GeneType.Discrete));
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, 1, GeneType.Continuous));
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, 1, GeneType.Structural));
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, 1, GeneType.Parametric));
    }

    [Fact]
    public void GeneAttribute_Min_Greater_Than_Max_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeneAttribute(100, 0));
    }
}
