// -----------------------------------------------------------------------------
// <copyright file="ConfigStore.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Core;

using System.Collections.Concurrent;

/// <summary>
/// In-memory configuration store for runtime settings.
/// </summary>
internal sealed class ConfigStore
{
    private readonly ConcurrentDictionary<string, object?> _config = new();

    /// <summary>Gets a configuration value by key.</summary>
    public object? Get(string key)
    {
        _config.TryGetValue(key, out var value);
        return value;
    }

    /// <summary>Sets a configuration value by key.</summary>
    public void Set(string key, object? value)
    {
        _config[key] = value;
    }

    /// <summary>Gets all configuration as a dictionary.</summary>
    public IReadOnlyDictionary<string, object?> GetAll()
    {
        return new Dictionary<string, object?>(_config);
    }
}
