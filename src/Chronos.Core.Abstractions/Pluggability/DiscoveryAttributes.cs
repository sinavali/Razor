namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Marks a strategy class with its canonical name for automatic discovery.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StrategyNameAttribute : Attribute
{
    /// <summary>The strategy name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    /// <param name="name">The strategy name.</param>
    public StrategyNameAttribute(string name) => Name = name;
}

/// <summary>Marks a risk manager class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RiskManagerNameAttribute : Attribute
{
    /// <summary>The risk manager name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public RiskManagerNameAttribute(string name) => Name = name;
}

/// <summary>Marks a position sizer class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PositionSizerNameAttribute : Attribute
{
    /// <summary>The position sizer name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public PositionSizerNameAttribute(string name) => Name = name;
}

/// <summary>Marks a market regime detector class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MarketRegimeDetectorNameAttribute : Attribute
{
    /// <summary>The market regime detector name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public MarketRegimeDetectorNameAttribute(string name) => Name = name;
}

/// <summary>Marks an execution algorithm class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ExecutionAlgoNameAttribute : Attribute
{
    /// <summary>The execution algorithm name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public ExecutionAlgoNameAttribute(string name) => Name = name;
}

/// <summary>Marks a notification channel class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotificationChannelNameAttribute : Attribute
{
    /// <summary>The notification channel name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public NotificationChannelNameAttribute(string name) => Name = name;
}

/// <summary>Marks a metrics provider class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MetricsProviderNameAttribute : Attribute
{
    /// <summary>The metrics provider name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public MetricsProviderNameAttribute(string name) => Name = name;
}

/// <summary>Marks a report generator class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ReportGeneratorNameAttribute : Attribute
{
    /// <summary>The report generator name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public ReportGeneratorNameAttribute(string name) => Name = name;
}

/// <summary>Marks a fitness model class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FitnessModelNameAttribute : Attribute
{
    /// <summary>The fitness model name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public FitnessModelNameAttribute(string name) => Name = name;
}

/// <summary>Marks a market data provider class with its canonical name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MarketDataProviderNameAttribute : Attribute
{
    /// <summary>The market data provider name.</summary>
    public string Name { get; }
    /// <summary>Creates a new attribute instance.</summary>
    public MarketDataProviderNameAttribute(string name) => Name = name;
}
