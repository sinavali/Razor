namespace Chronos.Core.Sdk.Hooks;

/// <summary>
/// Registration point for a filter hook.
/// A filter hook transforms or rejects data as it flows through the pipeline.
/// </summary>
/// <typeparam name="T">The type of data being filtered.</typeparam>
public interface IFilterRegistration<T>
{
    /// <summary>
    /// Register a filter callback.
    /// </summary>
    /// <param name="callback">
    /// The filter function. Receives the current data value and the hook context.
    /// Returns a <see cref="FilterResult{T}"/> indicating whether to allow the data
    /// (possibly modified) or reject it with a reason.
    /// </param>
    /// <param name="priority">
    /// Execution priority (lower = earlier). Default is 100.
    /// Hooks with the same priority are ordered alphabetically by plugin name,
    /// then by registration order within the plugin.
    /// </param>
    void Register(Func<T, IHookContext, FilterResult<T>> callback, int priority = 100);
}
