using Chronos.Core.Abstractions.Slots;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Chronos.Core.Abstractions.Shared;

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
            return
            [
                .. t.GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(GeneAttribute)))
                    .OrderBy(p => p.GetCustomAttribute<GeneAttribute>()!.Order)
                    .ThenBy(p => p.Name)
            ];
        });
    }

    /// <summary>Extracts current gene values from a strategy instance (no neural weights).</summary>
    public static double[] ExtractGenes(object strategyInstance)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        var props = GetGeneProperties(strategyInstance.GetType());
        double[] genes = new double[props.Count];
        for (int i = 0; i < props.Count; i++)
        {
            genes[i] = Convert.ToDouble(props[i].GetValue(strategyInstance), CultureInfo.InvariantCulture);
        }

        return genes;
    }

    /// <summary>
    /// Creates a complete gene array for a simple (non‑optimised) backtest.
    /// Property defaults + deterministically initialised neural weights using the given seed.
    /// </summary>
    public static double[] ExtractAndInitializeGenes(object strategyInstance, INeuralNetworkModel? neuralNet, int seed)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        var propertyGenes = ExtractGenes(strategyInstance);
        if (neuralNet == null)
        {
            return propertyGenes;
        }

        int neuralGeneCount = neuralNet.ParameterCount;
        if (neuralGeneCount == 0)
        {
            return propertyGenes;
        }

        var rng = new CustomizedRandom(seed);
        double[] neuralGenes = new double[neuralGeneCount];
        for (int i = 0; i < neuralGeneCount; i++)
        {
            neuralGenes[i] = (rng.NextDouble() * 2.0) - 1.0;
        }

        return [.. propertyGenes, .. neuralGenes];
    }

    /// <summary>Injects gene values into the strategy's properties (clamped and stepped according to gene type).</summary>
    public static void InjectPropertyGenes(object strategyInstance, double[] genes)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        ArgumentNullException.ThrowIfNull(genes);

        var props = GetGeneProperties(strategyInstance.GetType());
        if (genes.Length < props.Count)
        {
            throw new ArgumentException($"Gene array too short. Expected at least {props.Count}, got {genes.Length}.");
        }

        for (int i = 0; i < props.Count; i++)
        {
            var attr = props[i].GetCustomAttribute<GeneAttribute>()!;
            double val = genes[i];

            switch (attr.Type)
            {
                case GeneType.Continuous:
                    val = Math.Clamp(val, attr.Min, attr.Max);
                    break;

                case GeneType.Discrete:
                    val = Math.Clamp(val, attr.Min, attr.Max);
                    if (attr.Step > 0)
                    {
                        double steps = (val - attr.Min) / attr.Step;
                        steps = Math.Round(steps, MidpointRounding.AwayFromZero);
                        val = attr.Min + steps * attr.Step;
                        val = Math.Clamp(val, attr.Min, attr.Max);
                    }

                    break;

                case GeneType.Categorical:
                    val = Math.Clamp(val, attr.Min, attr.Max);
                    val = Math.Round(val, MidpointRounding.AwayFromZero);
                    break;

                case GeneType.Parametric:
                case GeneType.Structural:
                    break;

                default:
                    throw new ConfigurationException(
                        $"Unsupported GeneType '{attr.Type}' on property '{props[i].Name}'.");
            }

            Type propType = props[i].PropertyType;
            object converted;
            try
            {
                if (propType == typeof(int))
                {
                    converted = (int)Math.Round(val, MidpointRounding.AwayFromZero);
                }
                else if (propType == typeof(long))
                {
                    converted = (long)Math.Round(val, MidpointRounding.AwayFromZero);
                }
                else if (propType == typeof(float))
                {
                    converted = (float)val;
                }
                else if (propType == typeof(decimal))
                {
                    converted = (decimal)val;
                }
                else
                {
                    converted = Convert.ChangeType(val, propType, CultureInfo.InvariantCulture);
                }
            }
            catch (InvalidCastException ex)
            {
                throw new ConfigurationException(
                    $"Cannot convert gene value to property type '{propType.FullName}' on property '{props[i].Name}'. " +
                    "Only primitive numeric types are supported.", ex);
            }

            props[i].SetValue(strategyInstance, converted);
        }
    }

    /// <summary>
    /// Extracts the gene metadata schema from a strategy type.
    /// Returns a read‑only list of <b>copies</b> of the attributes to preserve immutability.
    /// </summary>
    public static IReadOnlyList<GeneAttribute> ExtractSchema(Type strategyType)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        return _schemaCache.GetOrAdd(strategyType, t =>
        {
            return
            [
                .. t.GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(GeneAttribute)))
                    .OrderBy(p => p.GetCustomAttribute<GeneAttribute>()!.Order)
                    .ThenBy(p => p.Name)
                    .Select(p =>
                    {
                        var orig = p.GetCustomAttribute<GeneAttribute>()!;
                        return new GeneAttribute(orig.Min, orig.Max, orig.Step, orig.Type)
                        {
                            Name = orig.Name,
                            Order = orig.Order
                        };
                    })
            ];
        });
    }

    /// <summary>Injects neural weights from a gene array starting at offset.</summary>
    public static void InjectNeuralGenes(INeuralNetworkModel network, double[] genes, int offset)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(genes);
        int needed = network.ParameterCount;
        if (genes.Length - offset < needed)
        {
            throw new ArgumentException(
                $"Insufficient genes for neural network. Needed {needed}, available {genes.Length - offset}.");
        }

        double[] slice = new double[needed];
        Array.Copy(genes, offset, slice, 0, needed);
        network.LoadParameters(slice);
    }

    /// <summary>Complete injection: strategy properties + neural weights.</summary>
    public static void InjectAll(object strategyInstance, INeuralNetworkModel? neuralNet, double[] genes)
    {
        ArgumentNullException.ThrowIfNull(strategyInstance);
        ArgumentNullException.ThrowIfNull(genes);
        var props = GetGeneProperties(strategyInstance.GetType());
        int propCount = props.Count;
        InjectPropertyGenes(strategyInstance, genes);
        if (neuralNet != null && genes.Length > propCount)
        {
            InjectNeuralGenes(neuralNet, genes, propCount);
        }
    }

    /// <summary>Generates a random gene value within constraints using the given CustomizedRandom.</summary>
    public static double GenerateRandomGene(CustomizedRandom rng, double min, double max, double step)
    {
        ArgumentNullException.ThrowIfNull(rng);
        Debug.Assert(min <= max, "Gene min must be ≤ max.");
        if (step <= 0)
        {
            return min + rng.NextDouble() * (max - min);
        }

        int steps = (int)Math.Round((max - min) / step);
        return steps < 0 ? min : min + rng.Next(steps + 1) * step;
    }

    /// <summary>Builds the complete chromosome schema (properties + optional neural weights).</summary>
    public static IReadOnlyList<GeneAttribute> BuildCompleteSchema(Type strategyType, INeuralNetworkModel? neuralNet)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        var schema = ExtractSchema(strategyType).ToList();

        if (neuralNet != null)
        {
            int count = neuralNet.ParameterCount;
            for (int i = 0; i < count; i++)
            {
                schema.Add(new GeneAttribute(-1.0, 1.0, 0, GeneType.Parametric)
                { Name = $"NN_W{i}", Order = int.MaxValue });
            }
        }

        return schema;
    }
}
