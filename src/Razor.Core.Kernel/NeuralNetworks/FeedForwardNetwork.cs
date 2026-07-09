using Razor.Core.Sdk.Slots.NeuralNetwork;

namespace Razor.Core.Kernel.NeuralNetworks;

/// <summary>
/// A simple feed‑forward neural network with configurable layers.
/// Implements <see cref="INeuralNetworkModel"/> and can be used as a built‑in model.
/// </summary>
public sealed class FeedForwardNetwork : INeuralNetworkModel
{
    private readonly int[] _layerSizes;
    private readonly double[][] _weights;
    private readonly double[][] _biases;
    private readonly int _parameterCount;
    private readonly ActivationFunction _activation;

    private double[]? _lastInput;

    /// <inheritdoc/>
    public string ModelType => "FeedForward";

    /// <inheritdoc/>
    public int InputSize => _layerSizes[0];

    /// <inheritdoc/>
    public int OutputSize => _layerSizes[^1];

    /// <inheritdoc/>
    public int ParameterCount => _parameterCount;

    /// <summary>
    /// Activation function type.
    /// </summary>
    public enum ActivationFunction
    {
        /// <summary>Rectified Linear Unit: max(0, x).</summary>
        ReLU,

        /// <summary>Hyperbolic tangent: tanh(x), range [-1, 1].</summary>
        Tanh,

        /// <summary>Sigmoid: 1 / (1 + e^-x), range (0, 1).</summary>
        Sigmoid
    }

    /// <summary>
    /// Creates a new feed‑forward network with the specified layer sizes and activation.
    /// </summary>
    /// <param name="layerSizes">Sizes of each layer (input, hidden, output).</param>
    /// <param name="activation">Activation function to use for hidden layers.</param>
    /// <exception cref="ArgumentException">Thrown if layerSizes has fewer than 2 layers.</exception>
    public FeedForwardNetwork(int[] layerSizes, ActivationFunction activation = ActivationFunction.ReLU)
    {
        ArgumentNullException.ThrowIfNull(layerSizes);

        if (layerSizes.Length < 2)
        {
            throw new ArgumentException("At least input and output layers are required.", nameof(layerSizes));
        }

        _layerSizes = (int[])layerSizes.Clone();
        _activation = activation;

        // Initialize weights and biases with small random values (Xavier-like)
        _weights = new double[_layerSizes.Length - 1][];
        _biases = new double[_layerSizes.Length - 1][];

        int totalParams = 0;
        for (int i = 0; i < _layerSizes.Length - 1; i++)
        {
            int rows = _layerSizes[i + 1];
            int cols = _layerSizes[i];
            _weights[i] = new double[rows * cols];
            _biases[i] = new double[rows];
            totalParams += rows * cols + rows;
        }
        _parameterCount = totalParams;

        // IMP-01: Initialize parameters with Xavier initialization so the network works even without LoadParameters.
        InitializeRandom();
    }

    private void InitializeRandom()
    {
        // Use a fixed seed for reproducibility.
        var rng = new Razor.Core.Sdk.Shared.CustomizedRandom(12345);
        for (int layer = 0; layer < _layerSizes.Length - 1; layer++)
        {
            int inSize = _layerSizes[layer];
            int outSize = _layerSizes[layer + 1];
            double std = Math.Sqrt(2.0 / (inSize + outSize)); // Xavier standard deviation
            for (int i = 0; i < outSize; i++)
            {
                for (int j = 0; j < inSize; j++)
                {
                    _weights[layer][i * inSize + j] = (rng.NextDouble() * 2 - 1) * std;
                }
                _biases[layer][i] = (rng.NextDouble() * 2 - 1) * 0.01;
            }
        }
    }

    /// <inheritdoc/>
    public double[] Predict(double[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Length != InputSize)
        {
            throw new ArgumentException($"Input size mismatch: expected {InputSize}, got {inputs.Length}.");
        }

        double[] current = new double[inputs.Length];
        Array.Copy(inputs, current, inputs.Length);
        _lastInput = (double[])inputs.Clone();

        for (int layer = 0; layer < _layerSizes.Length - 1; layer++)
        {
            int inSize = _layerSizes[layer];
            int outSize = _layerSizes[layer + 1];
            double[] next = new double[outSize];

            for (int i = 0; i < outSize; i++)
            {
                double sum = _biases[layer][i];
                int weightBase = i * inSize;
                for (int j = 0; j < inSize; j++)
                {
                    sum += current[j] * _weights[layer][weightBase + j];
                }

                next[i] = (layer == _layerSizes.Length - 2) ? sum : Activate(sum);
            }

            current = next;
        }

        return current;
    }

    private double Activate(double x)
    {
        return _activation switch
        {
            ActivationFunction.ReLU => x > 0 ? x : 0,
            ActivationFunction.Tanh => Math.Tanh(x),
            ActivationFunction.Sigmoid => 1.0 / (1.0 + Math.Exp(-x)),
            _ => x
        };
    }

    /// <inheritdoc/>
    public void LoadParameters(double[] genes)
    {
        ArgumentNullException.ThrowIfNull(genes);

        if (genes.Length != _parameterCount)
        {
            throw new ArgumentException($"Parameter count mismatch: expected {_parameterCount}, got {genes.Length}.");
        }

        int idx = 0;
        for (int layer = 0; layer < _layerSizes.Length - 1; layer++)
        {
            int inSize = _layerSizes[layer];
            int outSize = _layerSizes[layer + 1];
            int weightCount = inSize * outSize;
            Array.Copy(genes, idx, _weights[layer], 0, weightCount);
            idx += weightCount;
            Array.Copy(genes, idx, _biases[layer], 0, outSize);
            idx += outSize;
        }
    }

    /// <inheritdoc/>
    public double[] ExportParameters()
    {
        double[] result = new double[_parameterCount];
        int idx = 0;
        for (int layer = 0; layer < _layerSizes.Length - 1; layer++)
        {
            int inSize = _layerSizes[layer];
            int outSize = _layerSizes[layer + 1];
            int weightCount = inSize * outSize;
            Array.Copy(_weights[layer], 0, result, idx, weightCount);
            idx += weightCount;
            Array.Copy(_biases[layer], 0, result, idx, outSize);
            idx += outSize;
        }
        return result;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // No internal state to reset for feed‑forward, but we could clear last input
        _lastInput = null;
    }

    /// <inheritdoc/>
    public byte[] SerializeState()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(_layerSizes.Length);
        foreach (int size in _layerSizes)
        {
            writer.Write(size);
        }
        writer.Write((int)_activation);
        double[] paramsArray = ExportParameters();
        writer.Write(paramsArray.Length);
        foreach (double d in paramsArray)
        {
            writer.Write(d);
        }
        return ms.ToArray();
    }

    /// <inheritdoc/>
    public void DeserializeState(byte[] state)
    {
        using var ms = new MemoryStream(state);
        using var reader = new BinaryReader(ms);
        int layerCount = reader.ReadInt32();
        int[] layerSizes = new int[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            layerSizes[i] = reader.ReadInt32();
        }
        ActivationFunction activation = (ActivationFunction)reader.ReadInt32();
        int paramCount = reader.ReadInt32();
        double[] paramsArray = new double[paramCount];
        for (int i = 0; i < paramCount; i++)
        {
            paramsArray[i] = reader.ReadDouble();
        }

        // Validate topology matches current instance
        if (layerSizes.Length != _layerSizes.Length)
        {
            throw new InvalidOperationException($"Layer count mismatch: expected {_layerSizes.Length}, got {layerSizes.Length}.");
        }
        for (int i = 0; i < layerSizes.Length; i++)
        {
            if (layerSizes[i] != _layerSizes[i])
            {
                throw new InvalidOperationException($"Layer {i} size mismatch: expected {_layerSizes[i]}, got {layerSizes[i]}.");
            }
        }
        if (activation != _activation)
        {
            throw new InvalidOperationException($"Activation function mismatch: expected {_activation}, got {activation}.");
        }

        LoadParameters(paramsArray);
    }
}
