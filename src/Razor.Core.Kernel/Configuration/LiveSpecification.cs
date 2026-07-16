using Razor.Core.Sdk.Shared;

namespace Razor.Core.Kernel.Configuration;

/// <summary>
/// Immutable specification for a live trading session.
/// </summary>
public sealed record LiveSpecification
{
    /// <summary>Unique magic number for order tagging.</summary>
    public required int MagicNumber { get; init; }

    /// <summary>Timeout in seconds for duplicate order rejection (in‑flight guard).</summary>
    public int OrderGuardTimeoutSeconds { get; init; } = 5;

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
    public static LiveSpecification CreateValidated(int magicNumber, int orderGuardTimeoutSeconds = 5)
    {
        var spec = new LiveSpecification
        {
            MagicNumber = magicNumber,
            OrderGuardTimeoutSeconds = orderGuardTimeoutSeconds
        };
        spec.Validate();
        return spec;
    }
}
