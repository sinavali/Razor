namespace Chronos.Core.Abstractions.Hooks;

/// <summary>
/// Registration point for an action hook with a typed data payload.
/// An action hook observes events but cannot modify or reject data.
/// </summary>
/// <typeparam name="T">The type of data passed to the action.</typeparam>
/// <remarks>
/// <para><b>Important:</b> The callback <b>must be synchronous</b>.
/// Do not use <c>async void</c> — exceptions inside an async void delegate cannot be caught
/// by the engine and will crash the process. If you need to perform asynchronous work
/// (e.g., I/O or HTTP calls), queue the work externally with its own exception handling.</para>
/// </remarks>
public interface IActionRegistration<T>
{
    /// <summary>
    /// Register an action callback.
    /// </summary>
    /// <param name="callback">
    /// The action function. Receives the event data and the hook context.
    /// Must be synchronous.
    /// </param>
    /// <param name="priority">
    /// Execution priority (lower = earlier). Default is 100.
    /// Hooks with the same priority are ordered alphabetically by plugin name,
    /// then by registration order within the plugin.
    /// </param>
    void Register(Action<T, IHookContext> callback, int priority = 100);
}

/// <summary>
/// Registration point for an action hook with no data payload.
/// The callback receives only the hook context.
/// </summary>
/// <remarks>
/// <para><b>Important:</b> The callback <b>must be synchronous</b>.
/// Do not use <c>async void</c> — exceptions inside an async void delegate cannot be caught
/// by the engine and will crash the process. If you need to perform asynchronous work
/// (e.g., I/O or HTTP calls), queue the work externally with its own exception handling.</para>
/// </remarks>
public interface IActionRegistration
{
    /// <summary>
    /// Register an action callback.
    /// </summary>
    /// <param name="callback">
    /// The action function. Receives only the hook context.
    /// Must be synchronous.
    /// </param>
    /// <param name="priority">
    /// Execution priority (lower = earlier). Default is 100.
    /// Hooks with the same priority are ordered alphabetically by plugin name,
    /// then by registration order within the plugin.
    /// </param>
    void Register(Action<IHookContext> callback, int priority = 100);
}
