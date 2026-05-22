namespace Chronos.Abstractions.Adapters;

/// <summary>
/// Marks an adapter class with its canonical name.
/// Use this attribute on adapter implementations so that an <see cref="IAdapterFactory"/>
/// can discover them automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AdapterNameAttribute : Attribute
{
    /// <summary>The adapter name.</summary>
    public string Name { get; }

    /// <summary>Creates a new attribute instance.</summary>
    /// <param name="name">The adapter name.</param>
    public AdapterNameAttribute(string name) => Name = name;
}