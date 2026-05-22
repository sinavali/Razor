using Chronos.Abstractions.Shared;

namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Describes the topology and type of a neural network to be used by a strategy.
/// </summary>
public sealed record NeuralNetworkSpecification
{
    /// <summary>Number of neurons per layer (input .. hidden .. output).</summary>
    public required int[] Topology { get; init; }

    /// <summary>Activation function for hidden layers.</summary>
    public ActivationFunction Activation { get; init; } = ActivationFunction.Tanh;

    /// <summary>Model type identifier for extensibility (e.g., "FeedForward", "LSTM").</summary>
    public string ModelType { get; init; } = "FeedForward";

    /// <summary>Validates that the topology is valid and throws <see cref="ConfigurationException"/> if not.</summary>
    public void Validate()
    {
        if (Topology == null || Topology.Length < 2)
            throw new ConfigurationException("Neural network topology must contain at least an input and output layer.");
        if (Topology.Any(l => l <= 0))
            throw new ConfigurationException("All topology layer sizes must be positive.");

        var validModelTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FeedForward", "LSTM" };
        if (!validModelTypes.Contains(ModelType))
            throw new ConfigurationException(
                $"Unknown ModelType '{ModelType}'. Valid values: {string.Join(", ", validModelTypes)}.");
    }
}