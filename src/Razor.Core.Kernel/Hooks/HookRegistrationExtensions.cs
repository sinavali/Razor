using Razor.Core.Sdk.Hooks;

namespace Razor.Core.Kernel.Hooks;

/// <summary>
/// Extension methods that allow invoking filter/action chains directly
/// on the registration interfaces.
/// </summary>
public static class HookRegistrationExtensions
{
    /// <summary>
    /// Invokes the filter chain using the entries stored in the registration.
    /// </summary>
    public static FilterResult<T> InvokeFilterChain<T>(
        this IFilterRegistration<T> registration,
        T initialData,
        IHookContext context)
    {
        if (registration is FilterRegistration<T> reg)
        {
            return HookInvoker.InvokeFilterChain(reg.Entries, initialData, context);
        }

        return FilterResult.Allow(initialData);
    }

    /// <summary>
    /// Invokes the typed action chain using the entries stored in the registration.
    /// </summary>
    public static void InvokeActionChain<T>(
        this IActionRegistration<T> registration,
        T data,
        IHookContext context)
    {
        if (registration is ActionRegistration<T> reg)
        {
            HookInvoker.InvokeActionChain(reg.Entries, data, context);
        }
    }

    /// <summary>
    /// Invokes the parameterless action chain using the entries stored in the registration.
    /// </summary>
    public static void InvokeActionChain(
        this IActionRegistration registration,
        IHookContext context)
    {
        if (registration is ActionRegistration reg)
        {
            HookInvoker.InvokeActionChain(reg.Entries, context);
        }
    }
}
