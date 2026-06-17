namespace Chronos.Core.Kernel.Pluggability;

/// <summary>A specific engine capability.</summary>
public sealed record CapabilityDescriptor
{
    /// <summary>Name of capability.</summary>
    public required string Name { get; init; }
    /// <summary>Version of capability.</summary>
    public required string Version { get; init; }
    /// <summary>Enabled state.</summary>
    public required bool IsEnabled { get; init; }
    /// <summary>Flags configuration.</summary>
    public IReadOnlyList<string> Flags { get; init; } = [];
}
