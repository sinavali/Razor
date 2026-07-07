using Chronos.Core.Abstractions.Hooks;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Entry for a registered typed action callback.
/// </summary>
public sealed class ActionEntry<T>
{
    /// <summary>The action callback.</summary>
    public required Action<T, IHookContext> Callback { get; init; }

    /// <summary>Execution priority (lower = earlier).</summary>
    public int Priority { get; init; }

    /// <summary>Name of the plugin that registered this callback.</summary>
    public string PluginName { get; init; } = string.Empty;
}

/// <summary>
/// Entry for a registered parameterless action callback.
/// </summary>
public sealed class ActionEntry
{
    /// <summary>The action callback.</summary>
    public required Action<IHookContext> Callback { get; init; }

    /// <summary>Execution priority (lower = earlier).</summary>
    public int Priority { get; init; }

    /// <summary>Name of the plugin that registered this callback.</summary>
    public string PluginName { get; init; } = string.Empty;
}

/// <summary>
/// Default implementation of <see cref="IActionRegistration{T}"/>.
/// </summary>
public sealed class ActionRegistration<T> : IActionRegistration<T>
{
    private readonly List<ActionEntry<T>> _entries = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// All registered entries, sorted by (priority, plugin name, insertion order).
    /// </summary>
    public IReadOnlyList<ActionEntry<T>> Entries
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
    public void Register(Action<T, IHookContext> callback, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_lock)
        {
            _entries.Add(new ActionEntry<T>
            {
                Callback = callback,
                Priority = priority,
                PluginName = string.Empty
            });
            SortEntries();
        }
    }

    internal void RegisterWithPlugin(string pluginName, Action<T, IHookContext> callback, int priority)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_lock)
        {
            _entries.Add(new ActionEntry<T>
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

/// <summary>
/// Default implementation of parameterless <see cref="IActionRegistration"/>.
/// </summary>
public sealed class ActionRegistration : IActionRegistration
{
    private readonly List<ActionEntry> _entries = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// All registered entries, sorted.
    /// </summary>
    public IReadOnlyList<ActionEntry> Entries
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
    public void Register(Action<IHookContext> callback, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_lock)
        {
            _entries.Add(new ActionEntry
            {
                Callback = callback,
                Priority = priority,
                PluginName = string.Empty
            });
            SortEntries();
        }
    }

    internal void RegisterWithPlugin(string pluginName, Action<IHookContext> callback, int priority)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_lock)
        {
            _entries.Add(new ActionEntry
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
