namespace Chronos.Core.Engine.Communication;

/// <summary>
/// Represents a message exchanged with the cloud.
/// </summary>
internal sealed class CloudMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string MessageType { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public bool Encrypted { get; set; }
    public object? Payload { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Represents a command received from the cloud.
/// </summary>
internal sealed class CloudCommand
{
    public int CommandId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public object? Parameters { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? CorrelationId { get; set; }
}
