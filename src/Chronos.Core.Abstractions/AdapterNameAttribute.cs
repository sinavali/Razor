namespace Chronos.Core.Abstractions;

/// <summary>
/// Declares a human‑readable name for an adapter implementation.
/// The engine uses this attribute to discover adapters by name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AdapterNameAttribute : Attribute
{
    /// <summary>The adapter name.</summary>
    public string Name { get; }

    /// <summary>Creates a new instance of the attribute.</summary>
    public AdapterNameAttribute(string name)
    {
        Name = name;
    }
}
