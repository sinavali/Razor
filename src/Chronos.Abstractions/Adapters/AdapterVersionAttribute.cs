namespace Chronos.Abstractions.Adapters;

/// <summary>
/// Declares the release version of an adapter assembly for diagnostics.
/// Apply at assembly level.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class AdapterVersionAttribute : Attribute
{
    /// <summary>The adapter version string (e.g., "1.2.3").</summary>
    public string Version { get; }

    /// <summary>Creates a new adapter version attribute.</summary>
    public AdapterVersionAttribute(string version) => Version = version;
}