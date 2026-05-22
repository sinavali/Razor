namespace Chronos.Abstractions.Plugins;

/// <summary>
/// Declares the Chronos SDK version that a plugin assembly targets.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ChronosSdkVersionAttribute : Attribute
{
    /// <summary>Target SDK version (e.g., "1.0.0").</summary>
    public string Version { get; }

    /// <summary>Creates a new attribute instance.</summary>
    public ChronosSdkVersionAttribute(string version) => Version = version;
}