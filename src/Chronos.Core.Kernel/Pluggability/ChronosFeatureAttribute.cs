namespace Chronos.Core.Kernel.Pluggability;

/// <summary>Marks an interface or class with a specific capability version requirement.</summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ChronosFeatureAttribute : Attribute
{
    /// <summary>Name of the feature.</summary>
    public string FeatureName { get; }
    /// <summary>Minimum version required.</summary>
    public string MinVersion { get; }

    /// <summary>Initializes a new instance.</summary>
    public ChronosFeatureAttribute(string featureName, string minVersion = "1.0.0")
    {
        FeatureName = featureName;
        MinVersion = minVersion;
    }
}
