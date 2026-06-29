// -----------------------------------------------------------------------------
// <copyright file="LiveConfiguration.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Tasks;

using System.Globalization;
using System.Text.Json;

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
    public required string AccountCurrency { get; init; }   // base account currency

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

        string adapterName = GetString(dict, "AdapterName");
        string strategyName = GetString(dict, "StrategyName");
        int magicNumber = GetInt(dict, "MagicNumber");
        double leverage = GetDouble(dict, "Leverage");
        double initialBalance = GetDouble(dict, "InitialBalance");
        string[] symbols = GetStringArray(dict, "Symbols");
        int orderGuardTimeoutSeconds = GetInt(dict, "OrderGuardTimeoutSeconds", 5);
        double stopOutLevel = GetDouble(dict, "StopOutLevel");
        int maxOpenPositions = GetInt(dict, "MaxOpenPositions");
        double[] genes = GetDoubleArray(dict, "Genes", Array.Empty<double>());
        string? neuralNetworkName = dict.TryGetValue("NeuralNetworkName", out object? nnObj) ? nnObj?.ToString() : null;
        string accountCurrency = GetString(dict, "AccountCurrency", "USD");

        return new LiveConfiguration
        {
            AdapterName = adapterName,
            StrategyName = strategyName,
            MagicNumber = magicNumber,
            Leverage = leverage,
            InitialBalance = initialBalance,
            Symbols = symbols,
            OrderGuardTimeoutSeconds = orderGuardTimeoutSeconds,
            StopOutLevel = stopOutLevel,
            MaxOpenPositions = maxOpenPositions,
            Genes = genes,
            NeuralNetworkName = neuralNetworkName,
            AccountCurrency = accountCurrency
        };
    }

    private static string GetString(Dictionary<string, object> dict, string key, string fallback = "")
    {
        if (dict.TryGetValue(key, out object? value) && value is string s)
        {
            return s;
        }
        return fallback;
    }

    private static string[] GetStringArray(Dictionary<string, object> dict, string key)
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
        throw new ArgumentException($"Missing or invalid array field: {key}");
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
