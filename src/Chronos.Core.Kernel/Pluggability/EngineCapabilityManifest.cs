namespace Chronos.Core.Kernel.Pluggability;

/// <summary>Engine capability manifest.</summary>
public sealed record EngineCapabilityManifest
{
    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }
    /// <summary>Kernel version.</summary>
    public required string KernelVersion { get; init; }
    /// <summary>Abstractions version.</summary>
    public required string AbstractionsVersion { get; init; }
    /// <summary>Runtime version.</summary>
    public required string RuntimeVersion { get; init; }
    /// <summary>Operating System.</summary>
    public required string OS { get; init; }
    /// <summary>Available capabilities.</summary>
    public required IReadOnlyList<CapabilityDescriptor> Capabilities { get; init; }
    /// <summary>Currently loaded plugins.</summary>
    public required IReadOnlyList<LoadedPlugin> LoadedPlugins { get; init; }
    /// <summary>Boot timestamp.</summary>
    public required DateTime BootTime { get; init; }
}
