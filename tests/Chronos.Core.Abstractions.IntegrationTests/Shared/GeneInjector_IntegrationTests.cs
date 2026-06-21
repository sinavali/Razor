using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;

namespace Chronos.Core.Abstractions.IntegrationTests.Shared;

public class GeneInjector_IntegrationTests
{
    private sealed class TestStrategy
    {
        [Gene(0, 100, 1, GeneType.Discrete)]
        public int Period { get; set; } = 30;

        [Gene(0.1, 2.0, 0, GeneType.Continuous)]
        public double Factor { get; set; } = 1.0;
    }

    private sealed class BigStrategy
    {
        [Gene(0, 100, 1, GeneType.Discrete)]
        public int P1
        {
            get; set;
        }
        [Gene(0, 200, 1, GeneType.Discrete)]
        public int P2
        {
            get; set;
        }
        [Gene(0, 300, 1, GeneType.Discrete)]
        public int P3
        {
            get; set;
        }
        [Gene(0, 400, 1, GeneType.Discrete)]
        public int P4
        {
            get; set;
        }
        [Gene(0, 500, 1, GeneType.Discrete)]
        public int P5
        {
            get; set;
        }
        [Gene(0, 100, 0, GeneType.Continuous)]
        public double C1
        {
            get; set;
        }
        [Gene(0, 200, 0, GeneType.Continuous)]
        public double C2
        {
            get; set;
        }
        [Gene(-10, 10, 0, GeneType.Parametric)]
        public double W1
        {
            get; set;
        }
        [Gene(-10, 10, 0, GeneType.Parametric)]
        public double W2
        {
            get; set;
        }
        [Gene(0, 50, 1, GeneType.Categorical)]
        public int Cat
        {
            get; set;
        }
    }

    private sealed class CategoricalOnlyStrategy
    {
        [Gene(0, 50, 1, GeneType.Categorical)]
        public int Cat { get; set; } = 0;
    }

    private sealed class TestNetwork : INeuralNetworkModel
    {
        private double[] _params = new double[3];
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

    private sealed class BigNetwork : INeuralNetworkModel
    {
        private double[] _params = new double[1000];
        public string ModelType => "Big";
        public int InputSize => 100;
        public int OutputSize => 10;
        public int ParameterCount => 1000;
        public double[] Predict(double[] inputs) => new double[10];
        public void LoadParameters(double[] genes) => Array.Copy(genes, _params, 1000);
        public double[] ExportParameters() => (double[])_params.Clone();
        public void Reset()
        {
        }
        public byte[] SerializeState() => [];
        public void DeserializeState(byte[] state)
        {
        }
    }

    private static readonly double[] TestInputs = { 0.5, 0.2 };
    private static readonly double[] genes = new double[] { 1.0 };
    private static readonly double[] genesArray = new double[] { 1.0 };
    private static readonly double[] genesArray0 = new double[] { 1.0 };
    private static readonly double[] genesArray1 = new double[] { 1.0 };
    private static readonly double[] genesArray2 = new double[] { 25.7 };

    [Fact]
    public void Inject_Genes_Into_Strategy_And_Network_Then_Forward()
    {
        var strat = new TestStrategy();
        var nn = new TestNetwork();

        double[] genes = GeneInjector.ExtractAndInitializeGenes(strat, nn, seed: 123);
        GeneInjector.InjectAll(strat, nn, genes);

        Assert.InRange(strat.Period, 0, 100);
        Assert.InRange(strat.Factor, 0.1, 2.0);

        double[] output = nn.Predict(TestInputs);
        Assert.Single(output);
    }

    [Fact]
    public void BuildCompleteSchema_Includes_Neural_Genes()
    {
        var nn = new TestNetwork();
        var schema = GeneInjector.BuildCompleteSchema(typeof(TestStrategy), nn);
        int propCount = GeneInjector.GetGeneProperties(typeof(TestStrategy)).Count;
        Assert.Equal(propCount + 3, schema.Count);
        Assert.All(schema.Skip(propCount), g => Assert.Equal(GeneType.Parametric, g.Type));
    }

    [Fact]
    public void Extract_And_Initialize_Deterministic_With_Same_Seed()
    {
        var strat1 = new TestStrategy { Period = 20, Factor = 1.5 };
        var strat2 = new TestStrategy { Period = 20, Factor = 1.5 };
        var nn1 = new TestNetwork();
        var nn2 = new TestNetwork();

        var genes1 = GeneInjector.ExtractAndInitializeGenes(strat1, nn1, 42);
        var genes2 = GeneInjector.ExtractAndInitializeGenes(strat2, nn2, 42);

        Assert.Equal(genes1, genes2);
    }

    [Fact]
    public void Inject_And_Export_Large_Network()
    {
        var strat = new BigStrategy();
        var nn = new BigNetwork();
        var genes = GeneInjector.ExtractAndInitializeGenes(strat, nn, 42);
        Assert.Equal(1010, genes.Length);

        GeneInjector.InjectAll(strat, nn, genes);
        var exported = nn.ExportParameters();
        Assert.Equal(1000, exported.Length);
    }

    [Fact]
    public void BuildCompleteSchema_Large()
    {
        var nn = new BigNetwork();
        var schema = GeneInjector.BuildCompleteSchema(typeof(BigStrategy), nn);
        Assert.Equal(1010, schema.Count);
    }

    [Fact]
    public void ExtractGenes_Null_Instance_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.ExtractGenes(null!));
    }

    [Fact]
    public void InjectAll_Null_Genes_Throws()
    {
        var strat = new TestStrategy();
        Assert.Throws<ArgumentNullException>(() => GeneInjector.InjectAll(strat, null, null!));
    }

    [Fact]
    public void InjectPropertyGenes_Unsupported_GeneType_Throws()
    {
        var strat = new TestStrategy();
        Assert.Throws<ArgumentException>(() => GeneInjector.InjectPropertyGenes(strat, genesArray));
    }

    [Fact]
    public void ExtractAndInitializeGenes_Null_Strategy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.ExtractAndInitializeGenes(null!, null, 42));
    }

    [Fact]
    public void InjectNeuralGenes_Offset_Exceeds_Length_Throws()
    {
        var nn = new TestNetwork();
        Assert.Throws<ArgumentException>(() => GeneInjector.InjectNeuralGenes(nn, genes, 1));
    }

    [Fact]
    public void BuildCompleteSchema_Null_StrategyType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.BuildCompleteSchema(null!, null));
    }

    [Fact]
    public void ExtractAndInitializeGenes_Null_Network_Returns_PropertyGenes()
    {
        var strat = new TestStrategy { Period = 10, Factor = 1.5 };
        var genes = GeneInjector.ExtractAndInitializeGenes(strat, null, 42);
        Assert.Equal(2, genes.Length);
        Assert.Equal(1.5, genes[0]);
        Assert.Equal(10.0, genes[1]);
    }

    [Fact]
    public void InjectAll_Null_Strategy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GeneInjector.InjectAll(null!, null, genesArray0));
        Assert.Throws<ArgumentNullException>(() => GeneInjector.InjectAll(null!, null, genesArray1));
    }

    [Fact]
    public void ExtractAndInitializeGenes_Null_Network_Returns_Properties_Only()
    {
        var strat = new TestStrategy { Period = 10, Factor = 1.5 };
        var genes = GeneInjector.ExtractAndInitializeGenes(strat, null, 42);
        Assert.Equal(2, genes.Length);
        Assert.Equal(1.5, genes[0]);
        Assert.Equal(10.0, genes[1]);
    }

    [Fact]
    public void InjectPropertyGenes_Categorical_Clamps_And_Rounds()
    {
        var strat = new CategoricalOnlyStrategy();
        GeneInjector.InjectPropertyGenes(strat, genesArray2);
        Assert.Equal(26, strat.Cat);
    }

    [Fact]
    public void BuildCompleteSchema_Null_NeuralNet_Returns_Only_Property_Schema()
    {
        var schema = GeneInjector.BuildCompleteSchema(typeof(TestStrategy), null);
        Assert.Equal(2, schema.Count);
    }
}
