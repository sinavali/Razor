using Razor.Core.Engine.Extensions;

namespace Razor.Core.Engine.Pluggability;

/// <summary>Engine capability manifest.</summary>
internal sealed record EngineCapabilityManifest
{
    /// <summary>Gets the engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Gets the kernel version.</summary>
    public required string KernelVersion { get; init; }

    /// <summary>Gets the SDK version.</summary>
    public required string SdkVersion { get; init; }

    /// <summary>Gets the runtime version.</summary>
    public required string RuntimeVersion { get; init; }

    /// <summary>Gets the operating system.</summary>
    public required string OS { get; init; }

    /// <summary>Gets the available capabilities.</summary>
    public required IReadOnlyList<CapabilityDescriptor> Capabilities { get; init; }

    /// <summary>Gets the currently loaded plugins.</summary>
    public required IReadOnlyList<LoadedPlugin> LoadedPlugins { get; init; }

    /// <summary>Gets the boot timestamp.</summary>
    public required DateTime BootTime { get; init; }
}
