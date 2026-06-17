namespace Chronos.Core.Abstractions.Hooks;

/// <summary>
/// Base context provided to every hook callback.
/// </summary>
public interface IHookContext
{
    /// <summary>The name of the hook being invoked.</summary>
    string HookName { get; }

    /// <summary>The UTC time at the moment of invocation.</summary>
    DateTime UtcNow { get; }

    /// <summary>Cancellation token for the current operation.</summary>
    CancellationToken CancellationToken { get; }
}
