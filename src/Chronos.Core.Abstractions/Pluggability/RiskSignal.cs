namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Risk management signals.</summary>
public enum RiskSignal
{
    /// <summary>Allow operations</summary>
    Allow,
    /// <summary>Risk is elevated but tolerable. Engine continues but may log or notify.</summary>
    Warn,
    /// <summary>Halt all activity</summary>
    Halt
}
