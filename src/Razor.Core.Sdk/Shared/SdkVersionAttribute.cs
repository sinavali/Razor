namespace Razor.Core.Sdk.Shared;

/// <summary>
/// Declares the SDK version that a plugin assembly targets.
/// The engine validates this version before loading any assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class SdkVersionAttribute : Attribute
{
    /// <summary>Target SDK version (e.g., "1.0.0").</summary>
    public string Version { get; }

    /// <summary>Creates a new attribute instance.</summary>
    public SdkVersionAttribute(string version) => Version = version;
}
