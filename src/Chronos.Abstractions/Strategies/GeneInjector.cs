using Chronos.Abstractions.Shared;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Chronos.Abstractions.Strategies;

/// <summary>
/// Static helper for gene extraction, injection, and schema building.
/// </summary>
public static class GeneInjector
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> _propertyCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<GeneAttribute>> _schemaCache = new();

    /// <summary>Returns gene‑decorated properties ordered by <see cref="GeneAttribute.Order"/> and then name.</summary>
    public static IReadOnlyList<PropertyInfo> GetGeneProperties(Type strategyType)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        return _propertyCache.GetOrAdd(strategyType, t =>
        {
            return [.. t.GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(GeneAttribute)))
                .OrderBy(p => p.GetCustomAttribute<GeneAttribute>()!.Order)
                .ThenBy(p => p.Name)];
        });
    }

    /// <summary>Extracts current gene values from a strategy instance (no neural weights).</summary>
    public static double[] ExtractGenes(object strategyInstance)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        var props = GetGeneProperties(strategyInstance.GetType());
        double[] genes = new double[props.Count];
        for (int i = 0; i < props.Count; i++)
            genes[i] = Convert.ToDouble(props[i].GetValue(strategyInstance), CultureInfo.InvariantCulture);
        return genes;
    }

    /// <summary>
    /// Creates a complete gene array for a simple (non‑optimised) backtest.
    /// Property defaults + deterministically initialised neural weights using the given seed.
    /// </summary>
    public static double[] ExtractAndInitializeGenes(object strategyInstance, FeedForwardNetwork? neuralNet, int seed)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        var propertyGenes = ExtractGenes(strategyInstance);
        if (neuralNet == null) return propertyGenes;

        int neuralGeneCount = neuralNet.TotalGeneCount;
        if (neuralGeneCount == 0) return propertyGenes;

        var rng = new ChronosRandom(seed);
        double[] neuralGenes = new double[neuralGeneCount];
        for (int i = 0; i < neuralGeneCount; i++)
            neuralGenes[i] = (rng.NextDouble() * 2.0) - 1.0;

        return [.. propertyGenes, .. neuralGenes];
    }

    /// <summary>Injects gene values into the strategy's properties (clamped and stepped).</summary>
    public static void InjectPropertyGenes(object strategyInstance, double[] genes)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        ArgumentNullException.ThrowIfNull(genes);
        var props = GetGeneProperties(strategyInstance.GetType());
        if (genes.Length < props.Count)
            throw new ArgumentException($"Gene array too short. Expected at least {props.Count}, got {genes.Length}.");

        for (int i = 0; i < props.Count; i++)
        {
            var attr = props[i].GetCustomAttribute<GeneAttribute>()!;
            double val = Math.Clamp(genes[i], attr.Min, attr.Max);
            if (attr.Step > 0)
            {
                double steps = (val - attr.Min) / attr.Step;
                steps = Math.Round(steps, MidpointRounding.AwayFromZero);
                val = attr.Min + steps * attr.Step;
            }
            Type propType = props[i].PropertyType;
            object converted;
            if (propType == typeof(int)) converted = (int)Math.Round(val, MidpointRounding.AwayFromZero);
            else if (propType == typeof(long)) converted = (long)Math.Round(val, MidpointRounding.AwayFromZero);
            else if (propType == typeof(float)) converted = (float)val;
            else if (propType == typeof(decimal)) converted = (decimal)val;
            else converted = Convert.ChangeType(val, propType, CultureInfo.InvariantCulture);
            props[i].SetValue(strategyInstance, converted);
        }
    }

    /// <summary>Extracts the gene metadata schema from a strategy type.</summary>
    public static IReadOnlyList<GeneAttribute> ExtractSchema(Type strategyType)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        return _schemaCache.GetOrAdd(strategyType, t =>
        {
            return [.. t.GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(GeneAttribute)))
                .OrderBy(p => p.GetCustomAttribute<GeneAttribute>()!.Order)
                .ThenBy(p => p.Name)
                .Select(p => p.GetCustomAttribute<GeneAttribute>()!)];
        });
    }

    /// <summary>Appends neural weight genes to a schema.</summary>
    public static void AppendNeuralGenes(IList<GeneAttribute> schema, int[]? topology, ActivationFunction activation)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (topology == null || topology.Length < 2) return;

        var dummy = new FeedForwardNetwork(topology, activation);
        int count = dummy.TotalGeneCount;
        for (int i = 0; i < count; i++)
            schema.Add(new GeneAttribute(-1.0, 1.0, 0, GeneType.Parametric) { Name = $"NN_W{i}", Order = int.MaxValue });
    }

    /// <summary>Injects neural weights from a gene array starting at offset.</summary>
    public static void InjectNeuralGenes(FeedForwardNetwork network, double[] genes, int offset)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(genes);
        int needed = network.TotalGeneCount;
        if (genes.Length - offset < needed)
            throw new ArgumentException($"Insufficient genes for neural network. Needed {needed}, available {genes.Length - offset}.");
        double[] slice = new double[needed];
        Array.Copy(genes, offset, slice, 0, needed);
        network.InjectWeights(slice);
    }

    /// <summary>Complete injection: strategy properties + neural weights.</summary>
    public static void InjectAll(object strategyInstance, FeedForwardNetwork? neuralNet, double[] genes)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        ArgumentNullException.ThrowIfNull(genes);
        var props = GetGeneProperties(strategyInstance.GetType());
        int propCount = props.Count;
        InjectPropertyGenes(strategyInstance, genes);
        if (neuralNet != null && genes.Length > propCount)
            InjectNeuralGenes(neuralNet, genes, propCount);
    }

    /// <summary>Generates a random gene value within constraints using the given ChronosRandom.</summary>
    public static double GenerateRandomGene(ChronosRandom rng, double min, double max, double step)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (step <= 0)
            return min + rng.NextDouble() * (max - min);

        int steps = (int)Math.Round((max - min) / step);
        return steps < 0 ? min : min + rng.Next(steps + 1) * step;
    }

    /// <summary>
    /// Builds the complete chromosome schema (properties + optional neural weights).
    /// </summary>
    public static IReadOnlyList<GeneAttribute> BuildCompleteSchema(Type strategyType, int[]? neuralTopology, ActivationFunction activation)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        var schema = ExtractSchema(strategyType).ToList();
        AppendNeuralGenes(schema, neuralTopology, activation);
        return schema;
    }
}