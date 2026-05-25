namespace Chronos.Core.Abstractions.Strategies;

/// <summary>
/// A feed‑forward artificial neural network with configurable topology.
/// Genes (flat double array) can be injected to set weights and biases.
/// </summary>
public sealed class FeedForwardNetwork : INeuralNetwork
{
#pragma warning disable CA1819 // Properties should not return arrays
    /// <summary>Number of neurons per layer.</summary>
    public int[] Topology { get; }
    /// <summary>Neuron values per layer.</summary>
    public double[][] Neurons { get; }
    /// <summary>Weight matrices.</summary>
    public double[][][] Weights { get; }
    /// <summary>Bias vectors per layer (excluding input).</summary>
    public double[][] Biases { get; }
#pragma warning restore CA1819

    private readonly ActivationFunction _activation;

    /// <summary>Number of double values needed to represent all weights and biases.</summary>
    public int TotalGeneCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Topology.Length - 1; i++)
            {
                count += Topology[i] * Topology[i + 1];
                count += Topology[i + 1];
            }
            return count;
        }
    }

    /// <summary>
    /// Computes the total number of genes (weights + biases) for a given topology
    /// without instantiating a network.
    /// </summary>
    /// <param name="topology">Network topology (at least two layers).</param>
    /// <returns>Total number of double values needed to encode weights and biases.</returns>
    /// <exception cref="ArgumentException">Thrown if topology is null or has fewer than 2 layers.</exception>
    public static int GetTotalGeneCount(int[] topology)
    {
        if (topology is null || topology.Length < 2)
            throw new ArgumentException("Topology must have at least an input and output layer.", nameof(topology));

        int count = 0;
        for (int i = 0; i < topology.Length - 1; i++)
        {
            count += topology[i] * topology[i + 1]; // weights
            count += topology[i + 1];                // biases
        }
        return count;
    }

    /// <summary>Creates a new network with the given topology and activation function.</summary>
    public FeedForwardNetwork(int[] topology, ActivationFunction activation = ActivationFunction.Tanh)
    {
        if (topology == null || topology.Length < 2)
            throw new ArgumentException("Network topology must have at least an input and output layer.", nameof(topology));

        Topology = (int[])topology.Clone();
        _activation = activation;

        Neurons = new double[Topology.Length][];
        Weights = new double[Topology.Length - 1][][];
        Biases = new double[Topology.Length - 1][];

        for (int i = 0; i < Topology.Length; i++)
        {
            Neurons[i] = new double[Topology[i]];
            if (i < Topology.Length - 1)
            {
                Weights[i] = new double[Topology[i]][];
                for (int j = 0; j < Topology[i]; j++)
                    Weights[i][j] = new double[Topology[i + 1]];
                Biases[i] = new double[Topology[i + 1]];
            }
        }
    }

    /// <summary>
    /// Maps a flat gene array into the weight and bias matrices.
    /// </summary>
    public void InjectWeights(double[] dna)
    {
        ArgumentNullException.ThrowIfNull(dna);
        if (dna.Length != TotalGeneCount)
            throw new ArgumentException($"DNA length mismatch. Expected {TotalGeneCount}, got {dna.Length}.", nameof(dna));

        int ptr = 0;
        for (int i = 0; i < Topology.Length - 1; i++)
        {
            for (int j = 0; j < Topology[i + 1]; j++)
                Biases[i][j] = dna[ptr++];
            for (int j = 0; j < Topology[i]; j++)
            for (int k = 0; k < Topology[i + 1]; k++)
                Weights[i][j][k] = dna[ptr++];
        }
    }

    /// <inheritdoc/>
    public void InjectGenes(double[] genes) => InjectWeights(genes);

    /// <summary>Forward pass. Returns output layer values.</summary>
    public double[] FeedForward(double[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Length != Topology[0])
            throw new ArgumentException($"Input size mismatch. Expected {Topology[0]}, got {inputs.Length}.", nameof(inputs));

        Array.Copy(inputs, Neurons[0], inputs.Length);
        for (int i = 1; i < Topology.Length; i++)
        {
            for (int j = 0; j < Topology[i]; j++)
            {
                double sum = Biases[i - 1][j];
                for (int k = 0; k < Topology[i - 1]; k++)
                    sum += Neurons[i - 1][k] * Weights[i - 1][k][j];
                Neurons[i][j] = Activate(sum);
            }
        }
        return Neurons[^1];
    }

    private double Activate(double x) =>
        _activation switch
        {
            ActivationFunction.ReLU => x > 0 ? x : 0,
            ActivationFunction.LeakyReLU => x > 0 ? x : 0.01 * x,
            ActivationFunction.Sigmoid => 1.0 / (1.0 + Math.Exp(-x)),
            ActivationFunction.Tanh => Math.Tanh(x),
            ActivationFunction.Linear => x,
            _ => x
        };
}
