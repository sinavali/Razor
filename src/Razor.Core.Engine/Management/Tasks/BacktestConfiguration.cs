// -----------------------------------------------------------------------------
// <copyright file="BacktestConfiguration.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Tasks;
/// <summary>
/// Configuration for a backtest run, parsed from Cloud command parameters.
/// </summary>
internal sealed record BacktestConfiguration
{
    public required string AdapterName { get; init; }
    public required string StrategyName { get; init; }
    public required string[] Symbols { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required double InitialBalance { get; init; }
    public required double Leverage { get; init; }
    public required int MaxOpenPositions { get; init; }
    public required double StopOutLevel { get; init; }
    public required int MaxParallelThreads { get; init; }
    public required long LatencyTicks { get; init; }
    public required int WarmupWindowCount { get; init; }
    public required double[] Genes { get; init; }
    public int? GeneInitializationSeed { get; init; }
    public string? NeuralNetworkName { get; init; }
    public required string[] Timeframes { get; init; }
    public required string AccountCurrency { get; init; }

    /// <summary>
    /// Parses a Cloud command parameter object into a <see cref="BacktestConfiguration"/>.
    /// </summary>
    /// <param name="config">The configuration object (typically a dictionary).</param>
    /// <returns>A parsed configuration.</returns>
    /// <exception cref="ArgumentException">Thrown if required fields are missing or invalid.</exception>
    public static BacktestConfiguration Parse(object config)
    {
        if (config is not Dictionary<string, object> dict)
        {
            throw new ArgumentException("Configuration must be a dictionary.", nameof(config));
        }

        return new BacktestConfiguration
        {
            AdapterName = ConfigurationParser.GetString(dict, "AdapterName"),
            StrategyName = ConfigurationParser.GetString(dict, "StrategyName"),
            Symbols = ConfigurationParser.GetStringArray(dict, "Symbols"),
            StartDate = ConfigurationParser.GetDateTime(dict, "StartDate"),
            EndDate = ConfigurationParser.GetDateTime(dict, "EndDate"),
            InitialBalance = ConfigurationParser.GetDouble(dict, "InitialBalance"),
            Leverage = ConfigurationParser.GetDouble(dict, "Leverage"),
            MaxOpenPositions = ConfigurationParser.GetInt(dict, "MaxOpenPositions"),
            StopOutLevel = ConfigurationParser.GetDouble(dict, "StopOutLevel"),
            MaxParallelThreads = ConfigurationParser.GetInt(dict, "MaxParallelThreads", 0),
            LatencyTicks = ConfigurationParser.GetLong(dict, "LatencyTicks", 0),
            WarmupWindowCount = ConfigurationParser.GetInt(dict, "WarmupWindowCount", 0),
            Genes = ConfigurationParser.GetDoubleArray(dict, "Genes", Array.Empty<double>()),
            GeneInitializationSeed = dict.TryGetValue("GeneInitializationSeed", out object? seedObj) && seedObj is int seed ? seed : (int?)null,
            NeuralNetworkName = dict.TryGetValue("NeuralNetworkName", out object? nnObj) ? nnObj?.ToString() : null,
            Timeframes = ConfigurationParser.GetStringArray(dict, "Timeframes", Array.Empty<string>()),
            AccountCurrency = ConfigurationParser.GetString(dict, "AccountCurrency", "USD")
        };
    }
}
