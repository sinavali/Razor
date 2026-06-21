namespace Chronos.Core.Engine.Extensions;

/// <summary>
/// Describes an engine capability for feature negotiation.
/// </summary>
internal sealed record CapabilityDescriptor
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required bool IsEnabled { get; init; }
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
}
