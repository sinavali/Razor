using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots;

namespace Razor.Core.Sdk.UnitTests.Shared;

public class GeneInjectorTests
{
    internal sealed class StrategyWithGenes
    {
        [Gene(0, 100, 1, GeneType.Discrete)]
        public int Fast { get; set; } = 50;

        [Gene(0.1, 5.0, 1.0, GeneType.Discrete, Order = 1)]
        public double Risk { get; set; } = 2.0;

        public string NotGene { get; set; } = "ignore";
    }

    private sealed class MultiTypeStrategy
    {
        [Gene(0, 100, 1, GeneType.Discrete)]
        public int IntVal
        {
            get; set;
        }

        [Gene(0, 200, 1, GeneType.Discrete)]
        public long LongVal
        {
            get; set;
        }

        [Gene(0, 10, 0, GeneType.Continuous)]
        public float FloatVal
        {
            get; set;
        }

        [Gene(0, 10, 0, GeneType.Continuous)]
        public decimal DecimalVal
        {
            get; set;
        }
    }

    private sealed class NoGenes
    {
        public int NotGene
        {
            get; set;
        }
    }

    private sealed class SingleGeneStrategy
    {
        [Gene(0, 100, 0, GeneType.Continuous)]
        public double Value
        {
            get; set;
        }
    }

    private sealed class ParametricStrategy
    {
        [Gene(-10, 10, 0, GeneType.Parametric)]
        public double Weight { get; set; } = 0.5;
    }

    private sealed class StructuralStrategy
    {
        [Gene(0, 100, 0, GeneType.Structural)]
        public int Topology { get; set; } = 10;
    }

    private sealed class ContinuousGeneStrategy
    {
        [Gene(0, 100, 0, GeneType.Continuous)]
        public double Value { get; set; } = 50;
    }

    private sealed class TestNetwork : INeuralNetworkModel
    {
        private double[] _params = [];
        public string ModelType => "Test";
        public int InputSize => 2;
        public int OutputSize => 1;
        public int ParameterCount => 3;
        public double[] Predict(double[] inputs) => [0];
        public void LoadParameters(double[] genes) => _params = (double[])genes.Clone();
        public double[] ExportParameters() => (double[])_params.Clone();
        public void Reset()
        {
        }
        public byte[] SerializeState() => [];
        public void DeserializeState(byte[] state)
        {
        }
    }

    private sealed class ZeroParamNetwork : INeuralNetworkModel
    {
        public string ModelType => "Zero";
        public int InputSize => 1;
        public int OutputSize => 1;
        public int ParameterCount => 0;
        public double[] Predict(double[] inputs) => [0];
        public void LoadParameters(double[] genes)
        {
        }
        public double[] ExportParameters() => [];
        public void Reset()
        {
        }
        public byte[] SerializeState() => [];
        public void DeserializeState(byte[] state)
        {
        }
    }

    private static readonly double[] ClampGenes = { 99.0, 2.09 };
    private static readonly double[] ConversionGenes = { 2.71, 3.14, 12.6, 100.4 };
    private static readonly double[] InjectNeuralGenes = { 1.0, 2.0, 0.5, 0.1, 0.2 };
    private static readonly double[] InjectAllGenes = { 30, 3.0, 0.1, 0.2, 0.3 };
    private static readonly double[] NetworkInitParams = { 0.123, 0.456, 0.789 };
    private static readonly double[] ExpectedGenes = { 50.0, 2.0 };
    private static readonly double[] ExpectedPropertyGenes = { 10.0, 1.5 };
    private static readonly double[] ExpectedNeuralSlice = { 0.5, 0.1, 0.2 };

    // ── GetGeneProperties ────────────────────────────────────────

    [Fact]
    public void GetGeneProperties_NoGenes_Returns_Empty()
    {
        Assert.Empty(GeneInjector.GetGeneProperties(typeof(NoGenes)));
    }

    [Fact]
    public void GetGeneProperties_Returns_All_And_Ordered()
    {
        var props = GeneInjector.GetGeneProperties(typeof(StrategyWithGenes));
        Assert.Equal(2, props.Count);
        Assert.Equal("Fast", props[0].Name);
        Assert.Equal("Risk", props[1].Name);
    }

    // ── ExtractGenes ─────────────────────────────────────────────

    [Fact]
    public void ExtractGenes_NoGenes_Returns_Empty_Array()
    {
        Assert.Empty(GeneInjector.ExtractGenes(new NoGenes()));
    }

    [Fact]
    public void ExtractGenes_Current_Values()
    {
        var obj = new StrategyWithGenes { Fast = 50, Risk = 2.0 };
        var genes = GeneInjector.ExtractGenes(obj);
        Assert.Equal(ExpectedGenes, genes);
    }

    // ── InjectPropertyGenes ──────────────────────────────────────

    [Fact]
    public void InjectPropertyGenes_Clamps_And_Steps()
    {
        var obj = new StrategyWithGenes();
        GeneInjector.InjectPropertyGenes(obj, ClampGenes);
        Assert.Equal(99, obj.Fast);
        Assert.Equal(2.1, obj.Risk, 10);
    }

    [Fact]
    public void InjectPropertyGenes_Type_Conversion()
    {
        var obj = new MultiTypeStrategy();
        GeneInjector.InjectPropertyGenes(obj, ConversionGenes);
        Assert.Equal(2.71m, obj.DecimalVal);
        Assert.Equal(3.14f, obj.FloatVal, 5);
        Assert.Equal(13, obj.IntVal);
        Assert.Equal(100, obj.LongVal);
    }

    [Fact]
    public void InjectPropertyGenes_Parametric_Sets_Value_Directly()
    {
        var obj = new ParametricStrategy();
        GeneInjector.InjectPropertyGenes(obj, [7.5]);
        Assert.Equal(7.5, obj.Weight);
    }

    [Fact]
    public void InjectPropertyGenes_Structural_Sets_Value_Directly()
    {
        var obj = new StructuralStrategy();
        GeneInjector.InjectPropertyGenes(obj, [42.0]);
        Assert.Equal(42, obj.Topology);
    }

    [Fact]
    public void InjectPropertyGenes_Insufficient_Genes_Throws()
    {
        var obj = new ParametricStrategy();
        Assert.Throws<ArgumentException>(() => GeneInjector.InjectPropertyGenes(obj, Array.Empty<double>()));
    }

    // ── ExtractAndInitializeGenes ────────────────────────────────

    [Fact]
    public void ExtractAndInitializeGenes_Without_Network_Returns_PropertyGenes()
    {
        var obj = new StrategyWithGenes { Fast = 10, Risk = 1.5 };
        var genes = GeneInjector.ExtractAndInitializeGenes(obj, null, 42);
        Assert.Equal(ExpectedPropertyGenes, genes);
    }

    [Fact]
    public void ExtractAndInitializeGenes_With_Network_Appends_Random_Weights()
    {
        var obj = new StrategyWithGenes();
        var nn = new TestNetwork();
        var genes = GeneInjector.ExtractAndInitializeGenes(obj, nn, 42);
        Assert.Equal(5, genes.Length);
        for (int i = 2; i < genes.Length; i++)
        {
            Assert.InRange(genes[i], -1.0, 1.0);
        }
    }

    [Fact]
    public void ExtractAndInitializeGenes_With_Zero_Parameter_Network()
    {
        var obj = new ContinuousGeneStrategy { Value = 42 };
        var nn = new ZeroParamNetwork();
        var genes = GeneInjector.ExtractAndInitializeGenes(obj, nn, 42);
        Assert.Single(genes);
        Assert.Equal(42.0, genes[0]);
    }

    // ── ExtractSchema ────────────────────────────────────────────

    [Fact]
    public void ExtractSchema_Returns_Attributes()
    {
        var schema = GeneInjector.ExtractSchema(typeof(StrategyWithGenes));
        Assert.Equal(2, schema.Count);
    }

    [Fact]
    public void ExtractSchema_Caches_Result()
    {
        var schema1 = GeneInjector.ExtractSchema(typeof(SingleGeneStrategy));
        var schema2 = GeneInjector.ExtractSchema(typeof(SingleGeneStrategy));
        Assert.Same(schema1, schema2);
    }

    // ── InjectNeuralGenes ────────────────────────────────────────

    [Fact]
    public void InjectNeuralGenes_With_Offset()
    {
        var nn = new TestNetwork();
        GeneInjector.InjectNeuralGenes(nn, InjectNeuralGenes, 2);
        Assert.Equal(ExpectedNeuralSlice, nn.ExportParameters());
    }

    [Fact]
    public void InjectNeuralGenes_Insufficient_Genes_Throws()
    {
        var nn = new TestNetwork();
        Assert.Throws<ArgumentException>(() => GeneInjector.InjectNeuralGenes(nn, [1.0], 0));
    }

    [Fact]
    public void InjectNeuralGenes_Null_Network_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.InjectNeuralGenes(null!, [1.0], 0));
    }

    [Fact]
    public void InjectNeuralGenes_Exact_Length_Match()
    {
        var nn = new TestNetwork();
        GeneInjector.InjectNeuralGenes(nn, [1.0, 2.0, 3.0], 0);
        Assert.Equal([1.0, 2.0, 3.0], nn.ExportParameters());
    }

    // ── InjectAll ────────────────────────────────────────────────

    [Fact]
    public void InjectAll_Property_And_Neural()
    {
        var obj = new StrategyWithGenes();
        var nn = new TestNetwork();
        GeneInjector.InjectAll(obj, nn, InjectAllGenes);
        Assert.Equal(30, obj.Fast);
        Assert.Equal(3.1, obj.Risk, 10);
        Assert.Equal([0.1, 0.2, 0.3], nn.ExportParameters());
    }

    [Fact]
    public void InjectAll_Without_Neural_Genes_Leaves_Network_Untouched()
    {
        var strat = new SingleGeneStrategy();
        var nn = new TestNetwork();
        nn.LoadParameters(NetworkInitParams);
        GeneInjector.InjectAll(strat, nn, [99.0]);
        Assert.Equal(99.0, strat.Value);
        Assert.Equal(NetworkInitParams, nn.ExportParameters());
    }

    [Fact]
    public void InjectAll_Null_StrategyInstance_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.InjectAll(null!, null, [1.0]));
    }

    // ── GenerateRandomGene ───────────────────────────────────────

    [Fact]
    public void GenerateRandomGene_Continuous()
    {
        var rng = new CustomizedRandom(42);
        double val = GeneInjector.GenerateRandomGene(rng, 10, 20, 0);
        Assert.InRange(val, 10, 20);
    }

    [Fact]
    public void GenerateRandomGene_Discrete_Step()
    {
        var rng = new CustomizedRandom(42);
        double val = GeneInjector.GenerateRandomGene(rng, 0, 100, 10);
        Assert.True(val % 10 == 0);
    }

    // ── BuildCompleteSchema ──────────────────────────────────────

    [Fact]
    public void BuildCompleteSchema_Includes_Neural()
    {
        var nn = new TestNetwork();
        var schema = GeneInjector.BuildCompleteSchema(typeof(StrategyWithGenes), nn);
        Assert.Equal(5, schema.Count);
        Assert.All(schema.Skip(2), g => Assert.Equal(GeneType.Parametric, g.Type));
    }

    [Fact]
    public void BuildCompleteSchema_Without_Network_PropertiesOnly()
    {
        Assert.Equal(2, GeneInjector.BuildCompleteSchema(typeof(StrategyWithGenes), null).Count);
    }

    [Fact]
    public void BuildCompleteSchema_With_Zero_Parameter_Network()
    {
        var nn = new ZeroParamNetwork();
        var schema = GeneInjector.BuildCompleteSchema(typeof(ContinuousGeneStrategy), nn);
        Assert.Single(schema);
    }

    // ── Additional coverage ─────────────────────────────────────

    [Fact]
    public void InjectPropertyGenes_Categorical_Clamps_And_Rounds()
    {
        var obj = new CategoricalStrategy();
        GeneInjector.InjectPropertyGenes(obj, [7.8]);
        Assert.Equal(8, obj.Category);
    }

    [Fact]
    public void InjectPropertyGenes_Categorical_Clamps_To_Max()
    {
        var obj = new CategoricalStrategy();
        GeneInjector.InjectPropertyGenes(obj, [15.0]);
        Assert.Equal(10, obj.Category);
    }

    [Fact]
    public void InjectPropertyGenes_Continuous_Clamps_To_Min()
    {
        var obj = new ContinuousGeneStrategy();
        GeneInjector.InjectPropertyGenes(obj, [-5.0]);
        Assert.Equal(0, obj.Value);
    }

    [Fact]
    public void InjectPropertyGenes_Continuous_Clamps_To_Max()
    {
        var obj = new ContinuousGeneStrategy();
        GeneInjector.InjectPropertyGenes(obj, [150.0]);
        Assert.Equal(100, obj.Value);
    }

    [Fact]
    public void BuildCompleteSchema_Null_StrategyType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.BuildCompleteSchema(null!, null));
    }

    [Fact]
    public void ExtractAndInitializeGenes_Null_StrategyInstance_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.ExtractAndInitializeGenes(null!, null, 42));
    }

    [Fact]
    public void InjectAll_Null_Genes_Throws()
    {
        var obj = new CategoricalStrategy();
        Assert.Throws<ArgumentNullException>(() => GeneInjector.InjectAll(obj, null, null!));
    }

    [Fact]
    public void InjectNeuralGenes_Offset_Exceeds_Genes_Length_Throws()
    {
        var nn = new TestNetwork();
        Assert.Throws<ArgumentException>(() => GeneInjector.InjectNeuralGenes(nn, [1.0, 2.0], 1));
    }

    // Helper classes for new tests
    private sealed class CategoricalStrategy
    {
        [Gene(0, 10, 1, GeneType.Categorical)]
        public int Category { get; set; } = 5;
    }
}
