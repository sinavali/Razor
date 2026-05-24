namespace Chronos.Core.Abstractions.Strategies;

/// <summary>Activation functions.</summary>
public enum ActivationFunction
{
    /// <summary>1 / (1 + e^(-x))</summary>
    Sigmoid,
    /// <summary>tanh(x)</summary>
    Tanh,
    /// <summary>max(0, x)</summary>
    ReLU,
    /// <summary>max(0.01x, x)</summary>
    LeakyReLU,
    /// <summary>Linear (no activation).</summary>
    Linear
}
