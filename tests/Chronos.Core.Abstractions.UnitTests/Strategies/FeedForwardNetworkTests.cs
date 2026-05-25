using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

public class FeedForwardNetworkTests
{
    private static readonly int[] Topology2_3_1 = { 2, 3, 1 };
    private static readonly int[] Topology2_2 = { 2, 2 };
    private static readonly int[] Topology2_1 = { 2, 1 };
    private static readonly int[] Topology1_1 = { 1, 1 };

    private static readonly double[] Dna2_1 = { 0.5, 0.1, 0.2 };
    private static readonly double[] ZeroWeights2_1 = { 0, 0, 0 };
    private static readonly double[] SigmoidInputs2 = { 1, 1 };
    private static readonly double[] TanhInputs1 = { 0.0 };
    private static readonly double[] ReLUInputs1 = { 0.0 };
    private static readonly double[] LeakyReLUInputs1 = { 0.0 };
    private static readonly double[] LinearInputs1 = { 0.0 };

    // For 1-1 networks, DNA is [bias, weight]
    private static readonly double[] ReLUNegWeights = { -1.0, 0.0 };
    private static readonly double[] ReLUPosWeights = { 1.0, 0.0 };
    private static readonly double[] LeakyReLUNegWeights = { -1.0, 0.0 };
    private static readonly double[] LinearWeights = { 5.0, 0.0 };
    private static readonly double[] TanhWeights = { 0.5, 0.0 };
    private static readonly int[] topology = new[] { 5 };

    [Fact]
    public void Constructor_Invalid_Topology_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FeedForwardNetwork(null!));
        Assert.Throws<ArgumentException>(() => new FeedForwardNetwork(Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() => new FeedForwardNetwork(topology));
    }

    [Fact]
    public void GetTotalGeneCount_Valid_Topology()
    {
#pragma warning disable CA1861
        Assert.Equal(13, FeedForwardNetwork.GetTotalGeneCount(Topology2_3_1));
#pragma warning restore CA1861
    }

    [Fact]
    public void TotalGeneCount_Property_Matches_Static()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_3_1);
        Assert.Equal(FeedForwardNetwork.GetTotalGeneCount(Topology2_3_1), net.TotalGeneCount);
#pragma warning restore CA1861
    }

    [Fact]
    public void InjectWeights_Wrong_Length_Throws()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_2);
#pragma warning restore CA1861
        Assert.Throws<ArgumentException>(() => net.InjectWeights(new double[1]));
    }

    [Fact]
    public void InjectWeights_Null_Throws()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_2);
#pragma warning restore CA1861
        Assert.Throws<ArgumentNullException>(() => net.InjectWeights(null!));
    }

    [Fact]
    public void InjectWeights_Correct_Order()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_1);
#pragma warning restore CA1861
        net.InjectWeights(Dna2_1);
        Assert.Equal(0.5, net.Biases[0][0]);
        Assert.Equal(0.1, net.Weights[0][0][0]);
        Assert.Equal(0.2, net.Weights[0][1][0]);
    }

    [Fact]
    public void FeedForward_Sigmoid_Zero_Input_Zero_Weights()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_1, ActivationFunction.Sigmoid);
#pragma warning restore CA1861
        net.InjectWeights(ZeroWeights2_1);
        double[] output = net.FeedForward(SigmoidInputs2);
        Assert.Equal(0.5, output[0], 6);
    }

    [Fact]
    public void FeedForward_Tanh()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology1_1, ActivationFunction.Tanh);
#pragma warning restore CA1861
        net.InjectWeights(TanhWeights);
        Assert.Equal(Math.Tanh(0.5), net.FeedForward(TanhInputs1)[0], 12);
    }

    [Fact]
    public void FeedForward_ReLU()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology1_1, ActivationFunction.ReLU);
#pragma warning restore CA1861
        net.InjectWeights(ReLUNegWeights);
        Assert.Equal(0.0, net.FeedForward(ReLUInputs1)[0]);
        net.InjectWeights(ReLUPosWeights);
        Assert.Equal(1.0, net.FeedForward(ReLUInputs1)[0]);
    }

    [Fact]
    public void FeedForward_LeakyReLU()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology1_1, ActivationFunction.LeakyReLU);
#pragma warning restore CA1861
        net.InjectWeights(LeakyReLUNegWeights);
        Assert.Equal(-0.01, net.FeedForward(LeakyReLUInputs1)[0], 10);
    }

    [Fact]
    public void FeedForward_Linear()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology1_1, ActivationFunction.Linear);
#pragma warning restore CA1861
        net.InjectWeights(LinearWeights);
        Assert.Equal(5.0, net.FeedForward(LinearInputs1)[0]);
    }

    [Fact]
    public void FeedForward_Wrong_Input_Size_Throws()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_1);
#pragma warning restore CA1861
        Assert.Throws<ArgumentException>(() => net.FeedForward(new double[] { 1 }));
    }

    [Fact]
    public void InjectGenes_Delegates_To_InjectWeights()
    {
#pragma warning disable CA1861
        var net = new FeedForwardNetwork(Topology2_2);
#pragma warning restore CA1861
        double[] genes = new double[net.TotalGeneCount];
        genes[0] = 1.0;
        net.InjectGenes(genes);
        Assert.Equal(1.0, net.Biases[0][0]);
    }

    [Fact]
    public void Topology_Is_Cloned()
    {
        var topology = new[] { 2, 1 };
        var net = new FeedForwardNetwork(topology);
        topology[0] = 99;
        Assert.Equal(2, net.Topology[0]);
    }
}
