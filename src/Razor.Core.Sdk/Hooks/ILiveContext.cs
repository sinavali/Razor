using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.Hooks;

/// <summary>
/// Context provided to live trading hook callbacks.
/// </summary>
public interface ILiveContext : IHookContext
{
    /// <summary>The current tick being processed.</summary>
    Tick CurrentTick { get; }

    /// <summary>Current account equity.</summary>
    double CurrentEquity { get; }

    /// <summary>Current account balance.</summary>
    double CurrentBalance { get; }

    /// <summary>Current maximum drawdown percentage.</summary>
    double CurrentDrawdown { get; }

    /// <summary>The broker for the live session.</summary>
    IBroker Broker { get; }

    /// <summary>The name of the active adapter.</summary>
    string AdapterName { get; }

    /// <summary>Whether the adapter is currently connected.</summary>
    bool IsConnected { get; }
}
