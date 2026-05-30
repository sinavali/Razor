using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Abstractions.IntegrationTests.Strategies;

public class GeneInjector_IntegrationTests
{
    private sealed class TestStrategy
    {
        [Gene(0, 100, 10)]
        public int Period { get; set; } = 30;

        [Gene(0.1, 2.0, 0.1)]
        public double Factor { get; set; } = 1.0;
    }

    [Fact]
    public void Inject_Genes_Into_Strategy_And_Network_Then_Forward()
    {
        var strat = new TestStrategy();
        var nn = new FeedForwardNetwork(topologyArray, ActivationFunction.Tanh);

        // Build a complete gene array
        double[] genes = GeneInjector.ExtractAndInitializeGenes(strat, nn, seed: 123);
        GeneInjector.InjectAll(strat, nn, genes);

        // After injection, the strategy properties should reflect the first two genes
        // (after clamping/stepping). The neural weights are appended.
        Assert.InRange(strat.Period, 0, 100);
        Assert.InRange(strat.Factor, 0.1, 2.0);

        // Feed forward with some inputs, output must be within [-1,1] for tanh
        double[] output = nn.FeedForward(inputs);
        Assert.Single(output);
        Assert.InRange(output[0], -1.0, 1.0);
    }

    private static readonly int[] topology = new[] { 2, 1 };
    private static readonly int[] neuralTopology = new[] { 2, 1 };
    private static readonly double[] inputs = new double[] { 0.5, 0.2 };
    private static readonly int[] topologyArray = new[] { 2, 3, 1 };

    [Fact]
    public void BuildCompleteSchema_Includes_Neural_Genes()
    {
        var schema = GeneInjector.BuildCompleteSchema(typeof(TestStrategy), neuralTopology, ActivationFunction.Linear);
        int propCount = GeneInjector.GetGeneProperties(typeof(TestStrategy)).Count;
        int neuralCount = FeedForwardNetwork.GetTotalGeneCount(topology);
        Assert.Equal(propCount + neuralCount, schema.Count);
    }
}
