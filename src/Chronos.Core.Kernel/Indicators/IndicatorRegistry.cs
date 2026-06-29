using Chronos.Core.Abstractions.Shared;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Chronos.Core.Kernel.Indicators;

/// <summary>Default implementation of <see cref="IIndicatorRegistry"/> with compiled constructor cache and reference counting.</summary>
internal sealed class IndicatorRegistry : IIndicatorRegistry
{
    private readonly ConcurrentDictionary<string, IndicatorRef> _cache = new();
    private readonly TickWindow _tickWindow;

    private static readonly ConcurrentDictionary<(Type, int), Func<object[], object>> _ctorCache = new();

    public IndicatorRegistry(TickWindow tickWindow)
    {
        _tickWindow = tickWindow ?? throw new ArgumentNullException(nameof(tickWindow));
    }

    public IReadOnlyList<Indicator> ActiveIndicators => _cache.Values.Select(r => r.Instance).ToArray();

    public T Get<T>(params object[] args) where T : Indicator
    {
        string sig = typeof(T).FullName + ":" + string.Join(",", args.Select(a => a?.ToString() ?? "null"));

        if (_cache.TryGetValue(sig, out var existing))
        {
            existing.RefCount++;
            return (T)existing.Instance;
        }

        T instance;
        if (args.Length == 0)
        {
            instance = Activator.CreateInstance<T>();
        }
        else
        {
            var factory = GetOrCreateCtor(typeof(T), args.Length);
            instance = (T)factory(args);
        }

        instance.Signature = sig;
        var refObj = new IndicatorRef(instance);
        _cache[sig] = refObj;

        if (instance is IRegistryAwareIndicator aware)
        {
            aware.SetRegistry(this);
        }

        if (instance is IWindowAwareIndicator windowAware)
        {
            windowAware.SetWindow(_tickWindow);
        }

        return instance;
    }

    public bool Unregister(Indicator indicator)
    {
        if (indicator == null)
        {
            return false;
        }

        if (_cache.TryGetValue(indicator.Signature, out var refObj))
        {
            refObj.RefCount--;
            if (refObj.RefCount <= 0)
            {
                if (_cache.TryRemove(indicator.Signature, out _))
                {
                    indicator.Dispose();
                    return true;
                }
            }

            return true;
        }

        return false;
    }

    public void DisposeAll()
    {
        foreach (var refObj in _cache.Values)
        {
            refObj.Instance.Dispose();
        }

        _cache.Clear();
    }

    private static Func<object[], object> GetOrCreateCtor(Type type, int argCount)
    {
        return _ctorCache.GetOrAdd((type, argCount), key =>
        {
            var (t, n) = key;
            var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == n)
                ?? throw new InvalidOperationException(
                    $"No public constructor with {n} parameters found for {t.FullName}.");

            var argsParam = Expression.Parameter(typeof(object[]), "args");
            var parameters = ctor.GetParameters();
            var argExprs = new Expression[n];

            for (int i = 0; i < n; i++)
            {
                var paramType = parameters[i].ParameterType;
                var indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                var convertCall = Expression.Call(
                    typeof(Convert), nameof(Convert.ChangeType), null,
                    indexExpr, Expression.Constant(paramType));
                argExprs[i] = Expression.Convert(convertCall, paramType);
            }

            var newExpr = Expression.New(ctor, argExprs);
            var lambda = Expression.Lambda<Func<object[], object>>(newExpr, argsParam);
            return lambda.Compile();
        });
    }

    private sealed class IndicatorRef
    {
        public Indicator Instance { get; }
        public int RefCount { get; set; }

        public IndicatorRef(Indicator instance)
        {
            Instance = instance;
            RefCount = 1;
        }
    }
}
