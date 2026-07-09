namespace Razor.Core.Engine.Pluggability;

/// <summary>A loaded plugin state.</summary>
internal sealed record LoadedPlugin
{
    /// <summary>Gets the plugin ID.</summary>
    public required string PluginId { get; init; }

    /// <summary>Gets the plugin version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the plugin type.</summary>
    public required string PluginType { get; init; }

    /// <summary>Gets the SDK target version.</summary>
    public required string SdkVersion { get; init; }

    /// <summary>Gets whether the plugin is valid.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Gets the validation error reason if invalid.</summary>
    public string? ValidationError { get; init; }
}
