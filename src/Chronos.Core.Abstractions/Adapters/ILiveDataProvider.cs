namespace Chronos.Core.Abstractions.Adapters;

/// <summary>Provides live tick streaming.</summary>
public interface ILiveDataProvider
{
    /// <summary>Subscribes to tick updates for the given symbol.</summary>
    Task SubscribeAsync(string symbol);
    /// <summary>Unsubscribes from tick updates.</summary>
    Task UnsubscribeAsync(string symbol);
    /// <summary>Raised for every received tick.</summary>
    event Action<string, Chronos.Core.Abstractions.Shared.Tick> OnTickReceived;
}
