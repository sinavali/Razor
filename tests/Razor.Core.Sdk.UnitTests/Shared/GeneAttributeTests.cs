using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class GeneAttributeTests
{
    [Fact]
    public void Continuous_Step_Zero_Allowed()
    {
        var attr = new GeneAttribute(0, 1, 0, GeneType.Continuous);
        Assert.Equal(0, attr.Step);
    }

    [Fact]
    public void Continuous_Step_NonZero_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, 1, GeneType.Continuous));

    [Fact]
    public void Structural_Step_NonZero_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 100, 5, GeneType.Structural));

    [Fact]
    public void Parametric_Step_NonZero_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(-1, 1, 0.1, GeneType.Parametric));

    [Fact]
    public void Discrete_Step_LessThan_1_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 10, 0, GeneType.Discrete));

    [Fact]
    public void Categorical_Step_LessThan_1_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 5, 0, GeneType.Categorical));

    [Fact]
    public void Min_Greater_Than_Max_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(10, 5));

    [Fact]
    public void Negative_Step_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, -0.1));

    [Fact]
    public void Defaults_Name_Empty_Order_Zero()
    {
        var attr = new GeneAttribute(0, 1);
        Assert.Equal("", attr.Name);
        Assert.Equal(0, attr.Order);
    }

    [Fact]
    public void Name_And_Order_Can_Be_Set()
    {
        var attr = new GeneAttribute(0, 1) { Name = "MyGene", Order = 5 };
        Assert.Equal("MyGene", attr.Name);
        Assert.Equal(5, attr.Order);
    }
}
