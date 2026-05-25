using Chronos.Core.Abstractions.Strategies;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

public class GeneAttributeTests
{
    [Fact]
    public void Min_Greater_Than_Max_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(10, 5));

    [Fact]
    public void Negative_Step_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, -0.1));

    [Fact]
    public void Categorical_Step_Less_Than_1_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, 0, GeneType.Categorical));

    [Fact]
    public void Discrete_Step_Less_Than_1_Throws() =>
        Assert.Throws<ArgumentException>(() => new GeneAttribute(0, 1, 0, GeneType.Discrete));

    [Fact]
    public void Continuous_Step_Zero_Allowed()
    {
        _ = new GeneAttribute(0, 1, 0, GeneType.Continuous);
    }

    [Fact]
    public void Defaults_Name_Empty_Order_Zero()
    {
        var attr = new GeneAttribute(0, 1);
        Assert.Equal("", attr.Name);
        Assert.Equal(0, attr.Order);
    }
}

public class GeneInjectorTests
{
    internal sealed class StrategyWithGenes
    {
        [Gene(0, 100, 10)]
        public int Fast { get; set; } = 50;

        [Gene(0.1, 5.0, 0.1, Order = 1)]
        public double Risk { get; set; } = 2.0;

        public string NotGene { get; set; } = "ignore";
    }

    // Gene properties are extracted and ordered by Order, then alphabetically.
    // Without explicit Order, the alphabetical order is: DecimalVal, FloatVal, IntVal, LongVal.
    private sealed class MultiTypeStrategy
    {
        [Gene(0, 100)] public int IntVal { get; set; }
        [Gene(0, 200)] public long LongVal { get; set; }
        [Gene(0, 10, 0)] public float FloatVal { get; set; }    // step=0 to avoid stepping
        [Gene(0, 10, 0)] public decimal DecimalVal { get; set; } // step=0
    }

    private static readonly int[] NeuralTopology2_1 = { 2, 1 };
    private static readonly int[] NeuralTopology2_2 = { 2, 2 };

    private static readonly double[] GenesForClamp = { 99.0, 2.09 };
    // Order: DecimalVal, FloatVal, IntVal, LongVal.
    private static readonly double[] GenesForConversion = { 2.71, 3.14, 12.6, 100.4 };
    private static readonly double[] genes = new[] { 1.0 };

    [Fact]
    public void GetGeneProperties_Returns_All_And_Ordered()
    {
        var props = GeneInjector.GetGeneProperties(typeof(StrategyWithGenes));
        Assert.Equal(2, props.Count);
        Assert.Equal("Fast", props[0].Name);
        Assert.Equal("Risk", props[1].Name);
    }

    [Fact]
    public void ExtractGenes_Current_Values()
    {
        var obj = new StrategyWithGenes { Fast = 50, Risk = 2.0 };
        var genes = GeneInjector.ExtractGenes(obj);
        Assert.Equal(2, genes.Length);
        Assert.Equal(50.0, genes[0]);
        Assert.Equal(2.0, genes[1]);
    }

    [Fact]
    public void InjectPropertyGenes_Clamps_And_Steps()
    {
        var obj = new StrategyWithGenes();
        GeneInjector.InjectPropertyGenes(obj, GenesForClamp);
        Assert.Equal(100, obj.Fast);
        Assert.Equal(2.1, obj.Risk, 10);
    }

    [Fact]
    public void InjectPropertyGenes_Type_Conversion_Int_Long_Float_Decimal()
    {
        var obj = new MultiTypeStrategy();
        GeneInjector.InjectPropertyGenes(obj, GenesForConversion);
        Assert.Equal(13, obj.IntVal);                     // 12.6 stepped to 13
        Assert.Equal(100, obj.LongVal);                   // 100.4 stepped to 100
        Assert.Equal(3.14f, obj.FloatVal, 5);             // step=0, exact
        Assert.Equal(2.71m, obj.DecimalVal);              // step=0, exact
    }

    [Fact]
    public void InjectPropertyGenes_NonNumeric_Is_Converted_Via_ChangeType()
    {
        // Convert.ChangeType can convert double to string, so no exception is thrown.
        var obj = new BadPropStrategy();
        GeneInjector.InjectPropertyGenes(obj, genes);
        Assert.Equal("1", obj.Text);
    }

    private sealed class BadPropStrategy
    {
        [Gene(0, 1)] public string Text { get; set; } = "";
    }

    [Fact]
    public void ExtractAndInitializeGenes_Without_NeuralNet_Returns_PropertyGenes()
    {
        var obj = new StrategyWithGenes { Fast = 10, Risk = 1.5 };
        var genes = GeneInjector.ExtractAndInitializeGenes(obj, null, 42);
        Assert.Equal(2, genes.Length);
    }

    [Fact]
    public void ExtractAndInitializeGenes_With_NeuralNet_Appends_Random_Weights()
    {
        var obj = new StrategyWithGenes();
#pragma warning disable CA1861
        var nn = new FeedForwardNetwork(NeuralTopology2_1);
#pragma warning restore CA1861
        var genes = GeneInjector.ExtractAndInitializeGenes(obj, nn, 42);
        Assert.Equal(2 + nn.TotalGeneCount, genes.Length);
        for (int i = 2; i < genes.Length; i++)
        {
            Assert.InRange(genes[i], -1.0, 1.0);
        }
    }

    [Fact]
    public void ExtractSchema_Returns_Attributes()
    {
        var schema = GeneInjector.ExtractSchema(typeof(StrategyWithGenes));
        Assert.Equal(2, schema.Count);
    }

    [Fact]
    public void AppendNeuralGenes_Adds_Parametric_Genes()
    {
        var schema = new List<GeneAttribute>();
        GeneInjector.AppendNeuralGenes(schema, NeuralTopology2_1, ActivationFunction.Tanh);
        Assert.Equal(FeedForwardNetwork.GetTotalGeneCount(NeuralTopology2_1), schema.Count);
        Assert.All(schema, g => Assert.Equal(GeneType.Parametric, g.Type));
    }

    [Fact]
    public void AppendNeuralGenes_Null_Topology_Does_Nothing()
    {
        var schema = new List<GeneAttribute>();
        GeneInjector.AppendNeuralGenes(schema, null, ActivationFunction.Tanh);
        Assert.Empty(schema);
    }

    [Fact]
    public void InjectNeuralGenes_With_Offset()
    {
#pragma warning disable CA1861
        var nn = new FeedForwardNetwork(NeuralTopology2_1);
#pragma warning restore CA1861
        double[] genes = { 1.0, 2.0, 0.5, 0.1, 0.2 };
        GeneInjector.InjectNeuralGenes(nn, genes, 2);
        Assert.Equal(0.5, nn.Biases[0][0]);
        Assert.Equal(0.1, nn.Weights[0][0][0]);
        Assert.Equal(0.2, nn.Weights[0][1][0]);
    }

    [Fact]
    public void InjectNeuralGenes_Insufficient_Genes_Throws()
    {
#pragma warning disable CA1861
        var nn = new FeedForwardNetwork(NeuralTopology2_2);
#pragma warning restore CA1861
        Assert.Throws<ArgumentException>(() => GeneInjector.InjectNeuralGenes(nn, new double[2], 0));
    }

    [Fact]
    public void InjectAll_Property_And_Neural()
    {
        var obj = new StrategyWithGenes();
#pragma warning disable CA1861
        var nn = new FeedForwardNetwork(NeuralTopology2_1);
#pragma warning restore CA1861
        double[] genes = new double[2 + nn.TotalGeneCount];
        genes[0] = 30; genes[1] = 3.0;
        for (int i = 2; i < genes.Length; i++)
        {
            genes[i] = 0.1;
        }

        GeneInjector.InjectAll(obj, nn, genes);
        Assert.Equal(30, obj.Fast);
        Assert.Equal(3.0, obj.Risk, 10); // tolerance for floating point
        Assert.Equal(0.1, nn.Biases[0][0]);
    }

    [Fact]
    public void GenerateRandomGene_Continuous()
    {
        var rng = new ChronosRandom(42);
        double val = GeneInjector.GenerateRandomGene(rng, 10, 20, 0);
        Assert.InRange(val, 10, 20);
    }

    [Fact]
    public void GenerateRandomGene_Discrete_Step()
    {
        var rng = new ChronosRandom(42);
        double val = GeneInjector.GenerateRandomGene(rng, 0, 100, 10);
        Assert.True(val % 10 == 0);
    }

    // Removed GenerateRandomGene_Step_Negative_Steps_Returns_Min because it violates Debug.Assert(min <= max).

    [Fact]
    public void BuildCompleteSchema_Includes_Neural()
    {
#pragma warning disable CA1861
        var schema = GeneInjector.BuildCompleteSchema(typeof(StrategyWithGenes), NeuralTopology2_1, ActivationFunction.Tanh);
#pragma warning restore CA1861
        Assert.Equal(2 + FeedForwardNetwork.GetTotalGeneCount(NeuralTopology2_1), schema.Count);
    }
}
