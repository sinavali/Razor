using System.Collections.Concurrent;
using Chronos.Abstractions.Strategies;

namespace Chronos.Kernel.Indicators;

/// <summary>Default implementation of <see cref="IIndicatorRegistry"/>.</summary>
internal sealed class IndicatorRegistry : IIndicatorRegistry
{
    private readonly ConcurrentDictionary<string, Indicator> _cache = new();
    private readonly List<Indicator> _active = [];

    public IReadOnlyList<Indicator> ActiveIndicators => _active.AsReadOnly();

    public T Get<T>(params object[] args) where T : Indicator
    {
        string sig = typeof(T).FullName + ":" + string.Join(",", args.Select(a => a?.ToString() ?? "null"));
        if (_cache.TryGetValue(sig, out var existing) && existing is T typed)
        {
            return typed;
        }

        // ────── changed block ──────
        T instance;
        if (args.Length == 0)
        {
            instance = Activator.CreateInstance<T>();
        }
        else
        {
            // TODO: v1.1 – Cache compiled constructor delegates to avoid Activator.CreateInstance overhead.
            instance = (T)Activator.CreateInstance(typeof(T), args)!;
        }
        // ───────────────────────────

        instance.Signature = sig;
        _cache[sig] = instance;
        _active.Add(instance);

        if (instance is IRegistryAwareIndicator aware)
        {
            aware.SetRegistry(this);
        }

        return instance;
    }

    public bool Unregister(Indicator indicator)
    {
        if (indicator == null) return false;
        string sig = indicator.Signature;
        if (_cache.TryRemove(sig, out _))
        {
            _active.Remove(indicator);
            indicator.Dispose();
            return true;
        }
        return false;
    }

    public void DisposeAll()
    {
        foreach (var ind in _active)
        {
            ind.Dispose();
        }

        _active.Clear();
        _cache.Clear();
    }
}