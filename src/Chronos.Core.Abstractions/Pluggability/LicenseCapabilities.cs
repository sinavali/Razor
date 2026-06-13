using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Capabilities and limits dictated by the active license tier.</summary>
public sealed record LicenseCapabilities
{
    /// <summary>Maximum number of concurrent live engines.</summary>
    public required int MaxLiveEngines { get; init; }
    /// <summary>Are custom C# strategies permitted?</summary>
    public required bool AllowCustomStrategies { get; init; }
    /// <summary>Is continuous walk-forward optimization permitted?</summary>
    public required bool AllowContinuousOptimization { get; init; }
    /// <summary>Maximum lookback data limit in days.</summary>
    public required int MaxHistoryDays { get; init; }

    /// <summary>Validates the capabilities and throws if invalid.</summary>
    public void Validate()
    {
        if (MaxLiveEngines <= 0)
        {
            throw new ConfigurationException("MaxLiveEngines must be positive.");
        }

        if (MaxHistoryDays <= 0)
        {
            throw new ConfigurationException("MaxHistoryDays must be positive.");
        }
    }
}
