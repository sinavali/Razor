using System.Collections.Immutable;
using Chronos.Abstractions.Shared;

namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Immutable configuration for a trading strategy.
/// Specifies the account setup, symbols/timeframes, and optional friction / fitness models.
/// </summary>
public sealed record StrategySpecification
{
    /// <summary>Starting account balance in quote currency. Must be &gt; 0.</summary>
    public double InitialBalance { get; init; }

    /// <summary>Account‑wide leverage multiplier (e.g., 10, 50). Must be &gt; 0.</summary>
    public double Leverage { get; init; }

    /// <summary>Custom slippage/commission model for backtesting. If null, the adapter default is used.</summary>
    public ISimulationFriction? FrictionModel { get; init; }

    /// <summary>Scoring function for optimisation runs. Required for any GA operation.</summary>
    public IFitnessModel? FitnessModel { get; init; }

    /// <summary>Symbols and their timeframes the strategy needs. At least one element required.</summary>
    public ImmutableArray<SymbolRequest> RequestedSymbols { get; init; } = ImmutableArray<SymbolRequest>.Empty;

    /// <summary>Validates this specification and throws <see cref="ConfigurationException"/> if invalid.</summary>
    public void Validate()
    {
        if (InitialBalance <= 0)
            throw new ConfigurationException("InitialBalance must be positive.");
        if (Leverage <= 0)
            throw new ConfigurationException("Leverage must be positive.");
        if (RequestedSymbols.IsDefaultOrEmpty)
            throw new ConfigurationException("At least one symbol must be requested.");

        foreach (var sr in RequestedSymbols)
        {
            if (string.IsNullOrWhiteSpace(sr.Symbol))
                throw new ConfigurationException("SymbolRequest.Symbol must not be empty.");
            if (sr.TimeFrames.IsDefaultOrEmpty)
                throw new ConfigurationException(
                    $"SymbolRequest for '{sr.Symbol}' must specify at least one timeframe.");
        }
    }

    /// <summary>Creates a validated instance.</summary>
    public static StrategySpecification CreateValidated(
        double initialBalance,
        double leverage,
        ImmutableArray<SymbolRequest> requestedSymbols,
        ISimulationFriction? frictionModel = null,
        IFitnessModel? fitnessModel = null)
    {
        var spec = new StrategySpecification
        {
            InitialBalance = initialBalance,
            Leverage = leverage,
            RequestedSymbols = requestedSymbols,
            FrictionModel = frictionModel,
            FitnessModel = fitnessModel
        };
        spec.Validate();
        return spec;
    }
}
