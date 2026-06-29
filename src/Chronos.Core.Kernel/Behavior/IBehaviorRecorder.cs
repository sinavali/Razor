namespace Chronos.Core.Kernel.Behavior;

/// <summary>
/// Internal contract for recording behavioral data. Used by the engine and StrategyBase.
/// Not exposed to extension developers.
/// </summary>
internal interface IBehaviorRecorder
{
    bool IsEnabled { get; }
    void Enable(string sessionId, string strategyName, double[] genes, int uploadIntervalSeconds);
    void Disable();
    void Record(BehaviorRecord record);
    Task FlushAsync(CancellationToken cancellationToken);
    Task<object> GetLogsAsync(string sessionId, CancellationToken cancellationToken);
    Task DeleteLogsAsync(string sessionId, CancellationToken cancellationToken);
}
