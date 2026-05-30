using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Abstractions.IntegrationTests.Strategies;

public class FeedForwardNetwork_Large_IntegrationTests
{
    private static readonly int[] LargeTopology = { 50, 100, 1 };

    [Fact]
    public void Large_Network_Inject_And_Forward()
    {
        var nn = new FeedForwardNetwork(LargeTopology, ActivationFunction.Tanh);

        // Verify gene count
        int expectedGenes = FeedForwardNetwork.GetTotalGeneCount(LargeTopology);
        Assert.Equal(expectedGenes, nn.TotalGeneCount);

        // Fill with random-ish deterministic genes
        var rng = new ChronosRandom(42);
        double[] genes = new double[expectedGenes];
        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = (rng.NextDouble() * 2.0) - 1.0;
        }

        nn.InjectWeights(genes);

        // Feed forward with some inputs
        double[] inputs = new double[50];
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = i * 0.01;
        }

        double[] output = nn.FeedForward(inputs);

        Assert.Single(output);
        Assert.InRange(output[0], -1.0, 1.0); // tanh output
    }
}
