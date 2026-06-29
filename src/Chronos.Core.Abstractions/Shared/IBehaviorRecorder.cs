namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Contract for recording behavioral data from strategies.
/// The engine provides an implementation that buffers and uploads records to the cloud.
/// </summary>
public interface IBehaviorRecorder
{
    /// <summary>Indicates whether recording is currently enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Records a single behavior record asynchronously (buffered).</summary>
    void Record(BehaviorRecord record);
}
