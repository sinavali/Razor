using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Provides conversion rates from a symbol's quote currency to the account base currency.
/// Adapters that support cross‑currency margin/PnL should implement this.
/// </summary>
public interface ICurrencyConverter
{
    /// <summary>
    /// Gets the conversion rate from the symbol's quote currency to the account currency.
    /// </summary>
    /// <param name="symbol">The trading symbol (e.g., "EURUSD").</param>
    /// <param name="accountCurrency">The account base currency (e.g., "USD").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversion rate: 1 unit of symbol quote currency = rate * account currency.</returns>
    Task<double> GetConversionRateAsync(string symbol, string accountCurrency, CancellationToken cancellationToken = default);
}
