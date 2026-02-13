using System;
using System.Collections.Generic;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateFetcher
    {
        IReadOnlyList<ExchangeRateEntity> FetchAndStoreRates(ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime from, DateTime to);

        IReadOnlyList<ExchangeRateEntity> FetchAndStoreLatestRates(ExchangeRateSources source, ExchangeRateFrequencies frequency);
    }
}
