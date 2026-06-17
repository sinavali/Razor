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

    /// <summary>Notification channels.</summary>
    public ImmutableArray<INotificationChannel> NotificationChannels { get; init; } = [];

    /// <summary>Validates this specification and throws <see cref="ConfigurationException"/> if invalid.</summary>
    public void Validate()
    {
        if (MagicNumber <= 0)
        {
            throw new ConfigurationException("MagicNumber must be positive.");
        }

        if (OrderGuardTimeoutSeconds <= 0)
        {
            throw new ConfigurationException("OrderGuardTimeoutSeconds must be positive.");
        }
    }

    /// <summary>Creates a validated instance.</summary>
    public static LiveSpecification CreateValidated(
        int magicNumber,
        ImmutableArray<INotificationChannel>? notificationChannels = null)
    {
        var spec = new LiveSpecification
        {
            MagicNumber = magicNumber,
            NotificationChannels = notificationChannels ?? []
        };
        spec.Validate();
        return spec;
    }
}
