using MessagePack;

namespace Chronos.Core.Kernel.Behavior;

/// <summary>
/// Internal record for behavioral data. Not exposed to extension developers.
/// </summary>
#pragma warning disable MsgPack017 // default values on init properties are acceptable for our own serialization
[MessagePackObject(AllowPrivate = true)]
internal sealed record BehaviorRecord
{
    [Key(0)]
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    [Key(1)]
    public string SessionId { get; init; } = string.Empty;

    [Key(2)]
    public string StrategyName { get; init; } = string.Empty;

    [Key(3)]
    public string Action { get; init; } = string.Empty;

    [Key(4)]
    public double? Reward { get; init; }

    [Key(5)]
    public Dictionary<string, double> State { get; init; } = new();
}
#pragma warning restore MsgPack017
