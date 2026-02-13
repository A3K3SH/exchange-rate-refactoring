using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Exceptions;

namespace ExchangeRate.Core.Helpers
{
    public static class CurrencyCodeMapper
    {
        private static readonly Dictionary<string, CurrencyTypes> CurrencyMapping;

        static CurrencyCodeMapper()
        {
            var currencies = Enum.GetValues(typeof(CurrencyTypes)).Cast<CurrencyTypes>().ToList();
            CurrencyMapping = currencies.ToDictionary(x => x.ToString().ToUpperInvariant());
        }

        public static CurrencyTypes ParseCurrencyCode(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
                throw new ExchangeRateException("Null or empty currency code.");

            if (!CurrencyMapping.TryGetValue(currencyCode.ToUpperInvariant(), out var currency))
                throw new ExchangeRateException("Not supported currency code: " + currencyCode);

            return currency;
        }
    }
}
