using System;
using System.Collections.Generic;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using FluentResults;

namespace ExchangeRate.Core.Services
{
    public class ExchangeRateConversionService : IExchangeRateConversionService
    {
        public Result<decimal> TryGetRate(
            IReadOnlyDictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>> ratesByCurrencyAndDate,
            IReadOnlyDictionary<CurrencyTypes, PeggedCurrency> peggedCurrencies,
            DateTime date,
            DateTime minFxDate,
            IExchangeRateProvider provider,
            CurrencyTypes fromCurrency,
            CurrencyTypes toCurrency)
        {
            if (fromCurrency == toCurrency)
            {
                return Result.Ok(1m);
            }

            date = date.Date;

            if (fromCurrency != provider.Currency && toCurrency != provider.Currency)
            {
                var leftResult = TryGetRate(ratesByCurrencyAndDate, peggedCurrencies, date, minFxDate, provider, fromCurrency, provider.Currency);
                if (leftResult.IsFailed)
                    return leftResult;

                var rightResult = TryGetRate(ratesByCurrencyAndDate, peggedCurrencies, date, minFxDate, provider, provider.Currency, toCurrency);
                if (rightResult.IsFailed)
                    return rightResult;

                return Result.Ok(leftResult.Value * rightResult.Value);
            }

            var lookupCurrency = toCurrency == provider.Currency ? fromCurrency : toCurrency;
            var nonLookupCurrency = toCurrency == provider.Currency ? toCurrency : fromCurrency;

            if (!ratesByCurrencyAndDate.TryGetValue(lookupCurrency, out var currencyRates))
            {
                if (!peggedCurrencies.TryGetValue(lookupCurrency, out var peggedCurrency))
                {
                    return Result.Fail(new NotSupportedCurrencyError(lookupCurrency));
                }

                var peggedToCurrency = peggedCurrency.PeggedTo!.Value;
                var peggedToCurrencyResult = TryGetRate(ratesByCurrencyAndDate, peggedCurrencies, date, minFxDate, provider, nonLookupCurrency, peggedToCurrency);
                if (peggedToCurrencyResult.IsFailed)
                    return peggedToCurrencyResult;

                var peggedRate = peggedCurrency.Rate!.Value;
                var baseRate = peggedToCurrencyResult.Value;

                return Result.Ok(toCurrency == provider.Currency
                    ? peggedRate / baseRate
                    : baseRate / peggedRate);
            }

            var effectiveMinDate = minFxDate == DateTime.MaxValue ? date : minFxDate.Date;
            for (var d = date; d >= effectiveMinDate; d = d.AddDays(-1d))
            {
                if (currencyRates.TryGetValue(d, out var fxRate))
                {
                    return provider.QuoteType switch
                    {
                        QuoteTypes.Direct when toCurrency == provider.Currency => Result.Ok(fxRate),
                        QuoteTypes.Direct when fromCurrency == provider.Currency => Result.Ok(1 / fxRate),
                        QuoteTypes.Indirect when fromCurrency == provider.Currency => Result.Ok(fxRate),
                        QuoteTypes.Indirect when toCurrency == provider.Currency => Result.Ok(1 / fxRate),
                        _ => Result.Fail(new InvalidQuoteTypeError())
                    };
                }
            }

            return Result.Fail(new NoFxRateFoundError());
        }
    }

    public class NotSupportedCurrencyError : Error
    {
        public NotSupportedCurrencyError(CurrencyTypes currency)
            : base("Not supported currency: " + currency)
        {
            Currency = currency;
        }

        public CurrencyTypes Currency { get; }
    }

    public class NoFxRateFoundError : Error
    {
        public NoFxRateFoundError()
            : base("No fx rate found")
        {
        }
    }

    public class InvalidQuoteTypeError : Error
    {
        public InvalidQuoteTypeError()
            : base("Unsupported QuoteType")
        {
        }
    }
}
