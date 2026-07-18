namespace Razor.Core.Engine.Core.Exceptions;

/// <summary>
/// Thrown when a security violation is detected, such as a replayed or out-of-order
/// encrypted message that fails sequence validation.
/// </summary>
internal sealed class SecurityException : Exception
{
    public SecurityException() { }
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception inner) : base(message, inner) { }
}
