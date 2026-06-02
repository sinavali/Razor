namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Calculates the volume to trade based on account state and risk parameters.</summary>
public interface IPositionSizer
{
    /// <summary>Calculates position sizing volume.</summary>
    double CalculateVolume(PositionSizingContext context);
}
