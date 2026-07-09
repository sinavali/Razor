using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.Adapter;
using Razor.Core.Sdk.Slots.NeuralNetwork;
using Razor.Core.Sdk.Slots.Strategy;
using System.Reflection;

namespace Razor.Core.Engine.Extensions;

/// <summary>
/// Performs deep safety validation of plugin assemblies.
/// </summary>
internal static class PluginSafetyValidator
{
    public static IReadOnlyList<string> Validate(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var issues = new List<string>();

        // SEC‑03: Check assembly signing in production mode.
        if (Core.RuntimeEnvironment.IsProduction)
        {
            var publicKey = assembly.GetName().GetPublicKey();
            if (publicKey == null || publicKey.Length == 0)
            {
                issues.Add($"Assembly '{assembly.FullName}' is not strong‑named (unsigned). Unsigned extensions are rejected in production mode.");
                return issues; // Early return to avoid further checks on an unsigned assembly.
            }
        }

        var types = assembly.GetExportedTypes();

        foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract))
        {
            if (typeof(IAdapterCapability).IsAssignableFrom(type))
            {
                ValidateAdapter(type, issues);
            }
            if (typeof(IStrategyCapability).IsAssignableFrom(type))
            {
                ValidateStrategy(type, issues);
            }
            if (typeof(IHookManifest).IsAssignableFrom(type))
            {
                ValidateHookManifest(type, issues);
            }
            if (typeof(INeuralNetworkModel).IsAssignableFrom(type))
            {
                ValidateNeuralNetwork(type, issues);
            }
            if (typeof(Indicator).IsAssignableFrom(type))
            {
                ValidateIndicator(type, issues);
            }
        }

        return issues;
    }

    private static void ValidateAdapter(Type type, List<string> issues)
    {
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            issues.Add($"Adapter '{type.FullName}' lacks parameterless constructor.");
        }
        try
        {
            var instance = (IAdapterCapability)Activator.CreateInstance(type)!;
            if (string.IsNullOrWhiteSpace(instance.Name))
            {
                issues.Add($"Adapter '{type.FullName}' returned empty Name.");
            }

            if (instance.Calculator == null)
            {
                issues.Add($"Adapter '{type.FullName}' returned null Calculator.");
            }
        }
        catch (Exception ex)
        {
            issues.Add($"Adapter '{type.FullName}' instantiation failed: {ex.Message}");
        }
    }

    private static void ValidateStrategy(Type type, List<string> issues)
    {
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            issues.Add($"Strategy '{type.FullName}' lacks parameterless constructor.");
        }
    }

    private static void ValidateHookManifest(Type type, List<string> issues)
    {
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            issues.Add($"Hook manifest '{type.FullName}' lacks parameterless constructor.");
        }
    }

    private static void ValidateNeuralNetwork(Type type, List<string> issues)
    {
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            issues.Add($"Neural network '{type.FullName}' lacks parameterless constructor.");
        }
    }

    private static void ValidateIndicator(Type type, List<string> issues)
    {
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            issues.Add($"Indicator '{type.FullName}' lacks parameterless constructor.");
        }
    }
}
