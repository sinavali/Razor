namespace Chronos.Core.Abstractions.Slots;

/// <summary>
/// Capability interface for neural network models.
/// Supports feed‑forward, ONNX, LSTM, RL, and other architectures
/// through a unified parameter‑vector interface compatible with the GA.
/// Implementations are discovered in the <c>NeuralNetworks/</c> directory.
/// </summary>
public interface INeuralNetworkModel
{
    /// <summary>
    /// Human‑readable model type identifier (e.g., "FeedForward", "ONNX", "RL-DQN").
    /// </summary>
    string ModelType { get; }

    /// <summary>Number of input features the model expects.</summary>
    int InputSize { get; }

    /// <summary>Number of output values the model produces.</summary>
    int OutputSize { get; }

    /// <summary>
    /// Total number of double parameters (weights, biases, etc.) that the GA
    /// will include in the chromosome.
    /// </summary>
    int ParameterCount { get; }

    /// <summary>Perform a forward pass and return predictions.</summary>
    double[] Predict(double[] inputs);

    /// <summary>Load a flat parameter vector into the model's internal structure.</summary>
    void LoadParameters(double[] genes);

    /// <summary>Export the current parameters as a flat array.</summary>
    double[] ExportParameters();

    /// <summary>
    /// Reset any internal state (e.g., hidden states in LSTM, episode state in RL).
    /// Called before each backtest or evaluation run.
    /// </summary>
    void Reset();

    /// <summary>Serialize the full model state for save/restore.</summary>
    byte[] SerializeState();

    /// <summary>Deserialize the model state from a previously saved snapshot.</summary>
    void DeserializeState(byte[] state);
}
