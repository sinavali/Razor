using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Sdk.Slots.Adapter;

/// <summary>
/// Optional interface that adapters can implement to expose a currency converter.
/// </summary>
public interface IHasCurrencyConverter
{
    /// <summary>
    /// Gets the currency converter for this adapter.
    /// </summary>
    ICurrencyConverter CurrencyConverter { get; }
}
