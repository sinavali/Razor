namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Marker interface for any neural network model.
/// </summary>
public interface INeuralNetwork
{
    /// <summary>Injects a flat array of genes into the network's parameters.</summary>
    void InjectGenes(double[] genes);
    /// <summary>Performs a forward pass and returns the output.</summary>
    double[] FeedForward(double[] inputs);
}