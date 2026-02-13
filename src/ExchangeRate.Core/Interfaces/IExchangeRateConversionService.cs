using System;
using System.Collections.Generic;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces.Providers;
using FluentResults;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateConversionService
    {
        Result<decimal> TryGetRate(
            IReadOnlyDictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>> ratesByCurrencyAndDate,
            IReadOnlyDictionary<CurrencyTypes, PeggedCurrency> peggedCurrencies,
            DateTime date,
            DateTime minFxDate,
            IExchangeRateProvider provider,
            CurrencyTypes fromCurrency,
            CurrencyTypes toCurrency);
    }
}
