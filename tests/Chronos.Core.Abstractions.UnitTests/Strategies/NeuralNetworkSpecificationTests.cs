using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Abstractions.UnitTests.Strategies;

public class NeuralNetworkSpecificationTests
{
    [Fact]
    public void Validate_Valid_Topology_NoException()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 2, 3, 1 }, Activation = ActivationFunction.Tanh, ModelType = "FeedForward" };
        spec.Validate(); // no throw
    }

    [Fact]
    public void Validate_Null_Topology_Throws()
    {
        var spec = new NeuralNetworkSpecification { Topology = null! };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }

    [Fact]
    public void Validate_Topology_Less_Than_2_Layers_Throws()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 5 } };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }

    [Fact]
    public void Validate_Zero_Sized_Layer_Throws()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 2, 0, 1 } };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }

    [Fact]
    public void Validate_Negative_Size_Layer_Throws()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 2, -1, 1 } };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }

    [Fact]
    public void Validate_Invalid_ModelType_Throws()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 2, 1 }, ModelType = "CNN" };
        Assert.Throws<ConfigurationException>(() => spec.Validate());
    }

    [Fact]
    public void Validate_Accepts_LSTM()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 2, 1 }, ModelType = "LSTM" };
        spec.Validate(); // no throw
    }

    [Fact]
    public void Validate_Accepts_FeedForward_Case_Insensitive()
    {
        var spec = new NeuralNetworkSpecification { Topology = new[] { 2, 1 }, ModelType = "feedforward" };
        spec.Validate();
    }
}
