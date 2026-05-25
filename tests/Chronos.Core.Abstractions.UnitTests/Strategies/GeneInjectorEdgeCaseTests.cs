using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

public class GeneInjectorEdgeCaseTests
{
    private sealed class NoGenes
    {
        public int NotGene { get; set; }
    }

    private sealed class WithCategorical
    {
        [Gene(0, 10, 1, GeneType.Categorical)]
        public int Value { get; set; }
    }

    private sealed class WithStructural
    {
        [Gene(0, 100, 1, GeneType.Structural)]
        public int Value { get; set; }
    }

    private static readonly int[] Topology2_1 = { 2, 1 };
    private static readonly double[] CategoricalGene = new[] { 5.0 };
    private static readonly double[] StructuralGene = new[] { 50.0 };
    private static readonly int[] topology = new[] { 5 };

    [Fact]
    public void GetGeneProperties_NoGenes_Returns_Empty()
    {
        var props = GeneInjector.GetGeneProperties(typeof(NoGenes));
        Assert.Empty(props);
    }

    [Fact]
    public void ExtractGenes_NoGenes_Returns_Empty_Array()
    {
        var obj = new NoGenes();
        var genes = GeneInjector.ExtractGenes(obj);
        Assert.Empty(genes);
    }

    [Fact]
    public void InjectPropertyGenes_Categorical_Throws_ConfigurationException()
    {
        var obj = new WithCategorical();
        Assert.Throws<ConfigurationException>(() => GeneInjector.InjectPropertyGenes(obj, CategoricalGene));
    }

    [Fact]
    public void InjectPropertyGenes_Structural_Throws_ConfigurationException()
    {
        var obj = new WithStructural();
        Assert.Throws<ConfigurationException>(() => GeneInjector.InjectPropertyGenes(obj, StructuralGene));
    }

    [Fact]
    public void InjectAll_Without_Neural_Genes_Leaves_Network_Untouched()
    {
        var strat = new SingleGeneStrategy();
        var nn = new FeedForwardNetwork(Topology2_1);
        // Set some initial weights
        double[] initialDna = new double[nn.TotalGeneCount];
        initialDna[0] = 0.123;
        nn.InjectWeights(initialDna);

        // Inject only property genes (length 1)
        double[] genes = { 99.0 };
        GeneInjector.InjectAll(strat, nn, genes);
        Assert.Equal(99.0, strat.Value);
        // Neural weights should be unchanged
        Assert.Equal(0.123, nn.Biases[0][0]);
    }

    private sealed class SingleGeneStrategy
    {
        [Gene(0, 100)]
        public double Value { get; set; }
    }

    [Fact]
    public void ExtractSchema_Caches_Result()
    {
        var schema1 = GeneInjector.ExtractSchema(typeof(GeneInjectorTests.StrategyWithGenes));
        var schema2 = GeneInjector.ExtractSchema(typeof(GeneInjectorTests.StrategyWithGenes));
        Assert.Same(schema1, schema2);
    }

    [Fact]
    public void AppendNeuralGenes_Topology_Less_Than_2_Does_Nothing()
    {
        var schema = new List<GeneAttribute>();
        GeneInjector.AppendNeuralGenes(schema, topology, ActivationFunction.Tanh);
        Assert.Empty(schema);
    }
}
