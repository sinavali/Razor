using Chronos.Core.Abstractions.Hooks;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Entry for a registered filter callback, used for ordering and invocation.
/// </summary>
public sealed class FilterEntry<T>
{
    /// <summary>The filter callback.</summary>
    public required Func<T, IHookContext, FilterResult<T>> Callback { get; init; }

    /// <summary>Execution priority (lower = earlier).</summary>
    public int Priority { get; init; }

    /// <summary>Name of the plugin that registered this callback.</summary>
    public string PluginName { get; init; } = string.Empty;
}

/// <summary>
/// Default implementation of <see cref="IFilterRegistration{T}"/>.
/// </summary>
public sealed class FilterRegistration<T> : IFilterRegistration<T>
{
    private readonly List<FilterEntry<T>> _entries = new();
    private readonly object _lock = new();

    /// <summary>
    /// All registered entries, sorted by (priority, plugin name, insertion order).
    /// </summary>
    public IReadOnlyList<FilterEntry<T>> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritdoc/>
    public void Register(Func<T, IHookContext, FilterResult<T>> callback, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_lock)
        {
            _entries.Add(new FilterEntry<T>
            {
                Callback = callback,
                Priority = priority,
                PluginName = string.Empty
            });
            SortEntries();
        }
    }

    /// <summary>
    /// Registers a callback with a specific plugin name (called by the hook manifest loader).
    /// </summary>
    internal void RegisterWithPlugin(string pluginName, Func<T, IHookContext, FilterResult<T>> callback, int priority)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_lock)
        {
            _entries.Add(new FilterEntry<T>
            {
                Callback = callback,
                Priority = priority,
                PluginName = pluginName
            });
            SortEntries();
        }
    }

    /// <summary>
    /// Removes all entries registered with the specified plugin name.
    /// </summary>
    /// <param name="pluginName">The plugin name to remove.</param>
    public void RemoveAll(string pluginName)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e => string.Equals(e.PluginName, pluginName, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Removes all entries.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private void SortEntries()
    {
        _entries.Sort((a, b) =>
        {
            int cmp = a.Priority.CompareTo(b.Priority);
            if (cmp != 0)
            {
                return cmp;
            }

            return string.CompareOrdinal(a.PluginName, b.PluginName);
        });
    }
}
