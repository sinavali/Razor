namespace Chronos.Core.Abstractions.Shared;

/// <summary>Base exception for all Chronos errors.</summary>
public abstract class AppException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AppException"/> class.</summary>
    protected AppException() { }
    /// <summary>Initializes a new instance with a message.</summary>
    protected AppException(string message) : base(message) { }
    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    protected AppException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Exception originating from an adapter.</summary>
public class AdapterException : AppException
{
    /// <summary>Name of the adapter.</summary>
    public string AdapterName { get; }

    /// <summary>Initializes a new instance with adapter name and message.</summary>
    public AdapterException(string adapterName, string message)
        : base($"[{adapterName}] {message}") => AdapterName = adapterName;

    /// <summary>Initializes a new instance with adapter name, message, and inner exception.</summary>
    public AdapterException(string adapterName, string message, Exception inner)
        : base($"[{adapterName}] {message}", inner) => AdapterName = adapterName;
}

/// <summary>Exception originating from a strategy.</summary>
public class StrategyException : AppException
{
    /// <summary>Name of the strategy.</summary>
    public string StrategyName { get; }

    /// <summary>Initializes a new instance with strategy name and message.</summary>
    public StrategyException(string strategyName, string message)
        : base($"[{strategyName}] {message}") => StrategyName = strategyName;

    /// <summary>Initializes a new instance with strategy name, message, and inner exception.</summary>
    public StrategyException(string strategyName, string message, Exception inner)
        : base($"[{strategyName}] {message}", inner) => StrategyName = strategyName;
}

/// <summary>Exception thrown during genetic optimisation.</summary>
public class OptimizationException : AppException
{
    /// <summary>Initializes a new instance.</summary>
    public OptimizationException() { }
    /// <summary>Initializes a new instance with a message.</summary>
    public OptimizationException(string message) : base(message) { }
    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public OptimizationException(string message, Exception inner) : base(message, inner) { }
}
