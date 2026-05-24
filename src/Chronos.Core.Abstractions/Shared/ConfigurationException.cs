namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Exception thrown when an immutable specification contains invalid values.
/// </summary>
public sealed class ConfigurationException : Exception
{
    /// <summary>Creates a new instance with a message.</summary>
    public ConfigurationException(string message) : base(message) { }
    /// <summary>Creates a new instance with a message and inner exception.</summary>
    public ConfigurationException(string message, Exception inner) : base(message, inner) { }
}
