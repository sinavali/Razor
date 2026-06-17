using System.Collections.Immutable;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Immutable configuration for a trading strategy.
/// Specifies the account setup and symbols/timeframes the strategy needs.
/// </summary>
public sealed record StrategySpecification
{
    /// <summary>Starting account balance in quote currency. Must be &gt; 0.</summary>
    public required double InitialBalance { get; init; }

    /// <summary>Account‑wide leverage multiplier (e.g., 10, 50). Must be &gt; 0.</summary>
    public required double Leverage { get; init; }

    /// <summary>Symbols and their timeframes the strategy needs. At least one element required.</summary>
    public required ImmutableArray<SymbolRequest> RequestedSymbols { get; init; }

    /// <summary>Validates this specification and throws <see cref="ConfigurationException"/> if invalid.</summary>
    public void Validate()
    {
        if (InitialBalance <= 0)
        {
            throw new ConfigurationException("InitialBalance must be positive.");
        }

        if (Leverage <= 0)
        {
            throw new ConfigurationException("Leverage must be positive.");
        }

        if (RequestedSymbols.IsDefaultOrEmpty)
        {
            throw new ConfigurationException("At least one symbol must be requested.");
        }

        // Check for duplicate symbols (case‑insensitive).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sr in RequestedSymbols)
        {
            if (string.IsNullOrWhiteSpace(sr.Symbol))
            {
                throw new ConfigurationException("SymbolRequest.Symbol must not be empty.");
            }

            if (!seen.Add(sr.Symbol))
            {
                throw new ConfigurationException(
                    $"Duplicate symbol '{sr.Symbol}' in RequestedSymbols. Each symbol may only appear once.");
            }

            if (sr.TimeFrames.IsDefaultOrEmpty)
            {
                throw new ConfigurationException(
                    $"SymbolRequest for '{sr.Symbol}' must specify at least one timeframe.");
            }
        }
    }

    /// <summary>Creates a validated instance.</summary>
    public static StrategySpecification CreateValidated(
        double initialBalance,
        double leverage,
        ImmutableArray<SymbolRequest> requestedSymbols)
    {
        var spec = new StrategySpecification
        {
            InitialBalance = initialBalance,
            Leverage = leverage,
            RequestedSymbols = requestedSymbols
        };
        spec.Validate();
        return spec;
    }
}
