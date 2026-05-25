using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Chronos.Core.Abstractions.Strategies;

namespace Chronos.Core.Kernel.Indicators;

/// <summary>Default implementation of <see cref="IIndicatorRegistry"/> with compiled constructor cache.</summary>
internal sealed class IndicatorRegistry : IIndicatorRegistry
{
    private readonly ConcurrentDictionary<string, Indicator> _cache = new();
    private readonly List<Indicator> _active = [];

    // Compiled constructor cache: (Type, argCount) => factory delegate
    private static readonly ConcurrentDictionary<(Type, int), Func<object[], object>> _ctorCache = new();

    public IReadOnlyList<Indicator> ActiveIndicators => _active.AsReadOnly();

    public T Get<T>(params object[] args) where T : Indicator
    {
        string sig = typeof(T).FullName + ":" + string.Join(",", args.Select(a => a?.ToString() ?? "null"));
        if (_cache.TryGetValue(sig, out var existing) && existing is T typed)
        {
            return typed;
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

    /// <summary>
    /// Returns a compiled delegate that creates an instance of <paramref name="type"/>
    /// using a constructor with <paramref name="argCount"/> parameters.
    /// </summary>
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
                // Convert.ChangeType handles most primitive conversions
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
}
