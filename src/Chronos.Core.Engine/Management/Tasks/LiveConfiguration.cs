// -----------------------------------------------------------------------------
// <copyright file="LiveConfiguration.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Tasks;
/// <summary>
/// Configuration for a live trading session, parsed from Cloud command parameters.
/// </summary>
internal sealed record LiveConfiguration
{
    public required string AdapterName { get; init; }
    public required string StrategyName { get; init; }
    public required int MagicNumber { get; init; }
    public required double Leverage { get; init; }
    public required double InitialBalance { get; init; }
    public required string[] Symbols { get; init; }
    public required int OrderGuardTimeoutSeconds { get; init; }
    public required double StopOutLevel { get; init; }
    public required int MaxOpenPositions { get; init; }
    public required double[] Genes { get; init; }
    public string? NeuralNetworkName { get; init; }
    public required string AccountCurrency { get; init; }

    /// <summary>
    /// Parses a Cloud command parameter object into a <see cref="LiveConfiguration"/>.
    /// </summary>
    /// <param name="config">The configuration object (typically a dictionary).</param>
    /// <returns>A parsed configuration.</returns>
    /// <exception cref="ArgumentException">Thrown if required fields are missing or invalid.</exception>
    public static LiveConfiguration Parse(object config)
    {
        if (config is not Dictionary<string, object> dict)
        {
            throw new ArgumentException("Configuration must be a dictionary.", nameof(config));
        }

        return new LiveConfiguration
        {
            AdapterName = ConfigurationParser.GetString(dict, "AdapterName"),
            StrategyName = ConfigurationParser.GetString(dict, "StrategyName"),
            MagicNumber = ConfigurationParser.GetInt(dict, "MagicNumber"),
            Leverage = ConfigurationParser.GetDouble(dict, "Leverage"),
            InitialBalance = ConfigurationParser.GetDouble(dict, "InitialBalance"),
            Symbols = ConfigurationParser.GetStringArray(dict, "Symbols"),
            OrderGuardTimeoutSeconds = ConfigurationParser.GetInt(dict, "OrderGuardTimeoutSeconds", 5),
            StopOutLevel = ConfigurationParser.GetDouble(dict, "StopOutLevel"),
            MaxOpenPositions = ConfigurationParser.GetInt(dict, "MaxOpenPositions"),
            Genes = ConfigurationParser.GetDoubleArray(dict, "Genes", Array.Empty<double>()),
            NeuralNetworkName = dict.TryGetValue("NeuralNetworkName", out object? nnObj) ? nnObj?.ToString() : null,
            AccountCurrency = ConfigurationParser.GetString(dict, "AccountCurrency", "USD")
        };
    }
}
