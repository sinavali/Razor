// -----------------------------------------------------------------------------
// <copyright file="BacktestConfiguration.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Tasks;

using System.Globalization;
using System.Text.Json;

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

        string adapterName = GetString(dict, "AdapterName");
        string strategyName = GetString(dict, "StrategyName");
        string[] symbols = GetStringArray(dict, "Symbols");
        DateTime startDate = GetDateTime(dict, "StartDate");
        DateTime endDate = GetDateTime(dict, "EndDate");
        double initialBalance = GetDouble(dict, "InitialBalance");
        double leverage = GetDouble(dict, "Leverage");
        int maxOpenPositions = GetInt(dict, "MaxOpenPositions");
        double stopOutLevel = GetDouble(dict, "StopOutLevel");
        int maxParallelThreads = GetInt(dict, "MaxParallelThreads", 0);
        long latencyTicks = GetLong(dict, "LatencyTicks", 0);
        int warmupWindowCount = GetInt(dict, "WarmupWindowCount", 0);
        double[] genes = GetDoubleArray(dict, "Genes", Array.Empty<double>());
        int? geneInitializationSeed = dict.TryGetValue("GeneInitializationSeed", out object? seedObj) && seedObj is int seed ? seed : (int?)null;
        string? neuralNetworkName = dict.TryGetValue("NeuralNetworkName", out object? nnObj) ? nnObj?.ToString() : null;
        string[] timeframes = GetStringArray(dict, "Timeframes", Array.Empty<string>());

        return new BacktestConfiguration
        {
            AdapterName = adapterName,
            StrategyName = strategyName,
            Symbols = symbols,
            StartDate = startDate,
            EndDate = endDate,
            InitialBalance = initialBalance,
            Leverage = leverage,
            MaxOpenPositions = maxOpenPositions,
            StopOutLevel = stopOutLevel,
            MaxParallelThreads = maxParallelThreads,
            LatencyTicks = latencyTicks,
            WarmupWindowCount = warmupWindowCount,
            Genes = genes,
            GeneInitializationSeed = geneInitializationSeed,
            NeuralNetworkName = neuralNetworkName,
            Timeframes = timeframes
        };
    }

    private static string GetString(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object? value) && value is string s)
        {
            return s;
        }
        throw new ArgumentException($"Missing or invalid string field: {key}");
    }

    private static string[] GetStringArray(Dictionary<string, object> dict, string key, string[]? fallback = null)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is object[] array)
            {
                return array.Select(x => x.ToString()!).ToArray();
            }
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray().Select(x => x.GetString()!).ToArray();
            }
        }
        return fallback ?? Array.Empty<string>();
    }

    private static DateTime GetDateTime(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is DateTime dt)
            {
                return dt;
            }
            if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
        }
        throw new ArgumentException($"Missing or invalid DateTime field: {key}");
    }

    private static double GetDouble(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is double d)
            {
                return d;
            }
            if (value is int i)
            {
                return i;
            }
            if (value is long l)
            {
                return l;
            }
            if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }
        }
        throw new ArgumentException($"Missing or invalid double field: {key}");
    }

    private static int GetInt(Dictionary<string, object> dict, string key, int fallback = 0)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is int i)
            {
                return i;
            }
            if (value is long l)
            {
                return (int)l;
            }
            if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }

    private static long GetLong(Dictionary<string, object> dict, string key, long fallback = 0)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is long l)
            {
                return l;
            }
            if (value is int i)
            {
                return i;
            }
            if (value is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }

    private static double[] GetDoubleArray(Dictionary<string, object> dict, string key, double[] fallback)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is double[] arr)
            {
                return arr;
            }
            if (value is object[] objArr)
            {
                return objArr.Select(x => Convert.ToDouble(x, CultureInfo.InvariantCulture)).ToArray();
            }
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            }
        }
        return fallback;
    }
}
