using System.Collections.Immutable;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Kernel.Configuration;

/// <summary>
/// Immutable specification for a live trading session.
/// </summary>
public sealed record LiveSpecification
{
    /// <summary>Unique magic number for order tagging.</summary>
    public required int MagicNumber { get; init; }

    /// <summary>A Timeout for Order requests in seconds</summary>
    public int OrderGuardTimeoutSeconds { get; init; } = 5;

    /// <summary>Whether to run continuous background optimisation.</summary>
    public bool ContinuousOptimization { get; init; }

    /// <summary>Lookback days for continuous optimisation.</summary>
    public int LookbackDays { get; init; }

    /// <summary>Number of recent days to skip in continuous optimisation.</summary>
    public int SkipRecentDays { get; init; }

    /// <summary>Notification channels.</summary>
    public ImmutableArray<INotificationChannel> NotificationChannels { get; init; } = [];

    /// <summary>If true, the master seed will be rotated for each continuous optimisation cycle.</summary>
    public bool RotateOptimizationSeed { get; init; }

    /// <summary>Delay in minutes before the first continuous optimisation run.</summary>
    public int InitialDelayMinutes { get; init; } = 1;

    /// <summary>Interval in hours between automatic optimisations.</summary>
    public int OptimizationIntervalHours { get; init; } = 24;

    /// <summary>Validates this specification and throws <see cref="ConfigurationException"/> if invalid.</summary>
    public void Validate()
    {
        if (MagicNumber <= 0)
            throw new ConfigurationException("MagicNumber must be positive.");

        if (ContinuousOptimization)
        {
            if (LookbackDays <= 0)
                throw new ConfigurationException(
                    "LookbackDays must be positive when ContinuousOptimization is enabled.");
        }

        if (SkipRecentDays < 0)
            throw new ConfigurationException("SkipRecentDays cannot be negative.");

        if (InitialDelayMinutes < 0)
            throw new ConfigurationException("InitialDelayMinutes cannot be negative.");

        if (OptimizationIntervalHours <= 0)
            throw new ConfigurationException("OptimizationIntervalHours must be positive.");

        if (OrderGuardTimeoutSeconds <= 0)
            throw new ConfigurationException("OrderGuardTimeoutSeconds must be positive.");
    }

    /// <summary>Creates a validated instance.</summary>
    public static LiveSpecification CreateValidated(
        int magicNumber,
        bool continuousOptimization,
        int lookbackDays,
        int skipRecentDays,
        int initialDelayMinutes = 1,
        int optimizationIntervalHours = 24,
        ImmutableArray<INotificationChannel>? notificationChannels = null)
    {
        var spec = new LiveSpecification
        {
            MagicNumber = magicNumber,
            ContinuousOptimization = continuousOptimization,
            LookbackDays = lookbackDays,
            SkipRecentDays = skipRecentDays,
            InitialDelayMinutes = initialDelayMinutes,
            OptimizationIntervalHours = optimizationIntervalHours,
            NotificationChannels = notificationChannels ?? []
        };
        spec.Validate();
        return spec;
    }
}
