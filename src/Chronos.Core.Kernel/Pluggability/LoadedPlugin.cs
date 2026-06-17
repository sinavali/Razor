namespace Chronos.Core.Kernel.Pluggability;

/// <summary>A loaded plugin state.</summary>
public sealed record LoadedPlugin
{
    /// <summary>Plugin Id.</summary>
    public required string PluginId { get; init; }
    /// <summary>Plugin Version.</summary>
    public required string Version { get; init; }
    /// <summary>Type of the plugin.</summary>
    public required string PluginType { get; init; }
    /// <summary>SDK target version.</summary>
    public required string SdkVersion { get; init; }
    /// <summary>Is plugin valid.</summary>
    public required bool IsValid { get; init; }
    /// <summary>Validation error reason if invalid.</summary>
    public string? ValidationError { get; init; }
}
