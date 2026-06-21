namespace Chronos.Core.Engine.Pluggability;

/// <summary>Marks an interface or class with a specific capability version requirement.</summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class FeatureAttribute : Attribute
{
    /// <summary>Gets the feature name.</summary>
    public string FeatureName { get; }

    /// <summary>Gets the minimum version required.</summary>
    public string MinVersion { get; }

    /// <summary>Initializes a new instance of the <see cref="FeatureAttribute"/> class.</summary>
    /// <param name="featureName">The feature name.</param>
    /// <param name="minVersion">The minimum version.</param>
    public FeatureAttribute(string featureName, string minVersion = "1.0.0")
    {
        this.FeatureName = featureName;
        this.MinVersion = minVersion;
    }
}
