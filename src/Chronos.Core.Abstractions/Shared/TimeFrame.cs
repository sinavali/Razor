namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Time frame expressed in minutes. The integer value equals the duration in minutes.
/// </summary>
public enum TimeFrame
{
    /// <summary>Tick data (no aggregation).</summary>
    Tick = 0,

    /// <summary>1 minute.</summary>
    M1 = 1,

    /// <summary>5 minutes.</summary>
    M5 = 5,

    /// <summary>15 minutes.</summary>
    M15 = 15,

    /// <summary>30 minutes.</summary>
    M30 = 30,

    /// <summary>1 hour.</summary>
    H1 = 60,

    /// <summary>2 hours.</summary>
    H2 = 120,

    /// <summary>3 hours.</summary>
    H3 = 180,

    /// <summary>4 hours.</summary>
    H4 = 240,

    /// <summary>6 hours.</summary>
    H6 = 360,

    /// <summary>12 hours.</summary>
    H12 = 720,

    /// <summary>1 day.</summary>
    D1 = 1440,

    /// <summary>2 days.</summary>
    D2 = 2880,

    /// <summary>3 days.</summary>
    D3 = 4320,

    /// <summary>1 week.</summary>
    W1 = 10080,

    /// <summary>1 month (30 days).</summary>
    MN1 = 43200
}
