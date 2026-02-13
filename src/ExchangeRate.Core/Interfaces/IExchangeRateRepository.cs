using System;
using System.Collections.Generic;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateRepository
    {
        IReadOnlyDictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>> GetRates(ExchangeRateSources source, ExchangeRateFrequencies frequency);

        DateTime? GetMinRateDate(ExchangeRateSources source, ExchangeRateFrequencies frequency);

        void SaveRates(IEnumerable<ExchangeRateEntity> rates, bool overwriteExisting = true);

        IReadOnlyDictionary<CurrencyTypes, PeggedCurrency> GetPeggedCurrencies();
    }
}
