namespace Razor.Core.Engine.Core.Exceptions;

/// <summary>
/// Base exception for all engine‑specific errors.
/// </summary>
internal sealed class EngineException : Exception
{
    public EngineException() { }
    public EngineException(string message) : base(message) { }
    public EngineException(string message, Exception inner) : base(message, inner) { }
}
