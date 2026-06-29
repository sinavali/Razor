using Chronos.Core.Abstractions.Hooks;
using System.Diagnostics;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Static helper for invoking filter and action chains.
/// </summary>
public static class HookInvoker
{
    /// <summary>
    /// Invokes a chain of filter callbacks. Each callback receives the result of the previous one.
    /// If any callback returns <c>IsAllowed = false</c>, the chain stops and returns the rejection.
    /// </summary>
    public static FilterResult<T> InvokeFilterChain<T>(
        IReadOnlyList<FilterEntry<T>> entries,
        T initialData,
        IHookContext context)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(context);

        if (entries.Count == 0)
        {
            return FilterResult.Allow(initialData);
        }

        T? currentData = initialData;
        foreach (var entry in entries)
        {
            try
            {
                FilterResult<T> result = entry.Callback(currentData!, context);
                if (!result.IsAllowed)
                {
                    return result;
                }

                // Use null‑forgiving because result.IsAllowed guarantees Data is non‑null
                currentData = result.Data ?? currentData;
            }
#pragma warning disable CA1031 // Reason: Filters must not crash the pipeline; log and reject.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Trace.TraceError($"Filter hook {context.HookName} threw exception: {ex}");
                return FilterResult.Reject<T>($"Filter callback threw an exception: {ex.Message}");
            }
        }

        return FilterResult.Allow(currentData!);
    }

    /// <summary>
    /// Invokes a chain of typed action callbacks.
    /// Exceptions are caught and logged; they never stop the chain.
    /// </summary>
    public static void InvokeActionChain<T>(
        IReadOnlyList<ActionEntry<T>> entries,
        T data,
        IHookContext context)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entry in entries)
        {
            try
            {
                entry.Callback(data, context);
            }
#pragma warning disable CA1031 // Reason: Action hooks must not stop the pipeline.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Trace.TraceError($"Action hook {context.HookName} threw exception: {ex}");
            }
        }
    }

    /// <summary>
    /// Invokes a chain of parameterless action callbacks.
    /// Exceptions are caught and logged.
    /// </summary>
    public static void InvokeActionChain(
        IReadOnlyList<ActionEntry> entries,
        IHookContext context)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entry in entries)
        {
            try
            {
                entry.Callback(context);
            }
#pragma warning disable CA1031 // Reason: Action hooks must not stop the pipeline.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Trace.TraceError($"Action hook {context.HookName} threw exception: {ex}");
            }
        }
    }
}
