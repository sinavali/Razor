namespace Chronos.Abstractions.Adapters;

/// <summary>Data retention policy after a task completes.</summary>
public enum DataActionPolicy
{
    /// <summary>Keep the file until the host exits.</summary>
    KeepUntilExit,
    /// <summary>Delete the file immediately after the task finishes.</summary>
    DeleteAfterTask,
    /// <summary>Persist the file for later reuse.</summary>
    PersistentCache
}