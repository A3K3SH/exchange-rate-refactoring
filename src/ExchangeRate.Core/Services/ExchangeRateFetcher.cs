using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Exceptions;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using Microsoft.Extensions.Logging;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Services
{
    public class ExchangeRateFetcher : IExchangeRateFetcher
    {
        private readonly IExchangeRateProviderFactory _providerFactory;
        private readonly IExchangeRateRepository _repository;
        private readonly ILogger<ExchangeRateFetcher> _logger;

        public ExchangeRateFetcher(
            IExchangeRateProviderFactory providerFactory,
            IExchangeRateRepository repository,
            ILogger<ExchangeRateFetcher> logger)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<ExchangeRateEntity> FetchAndStoreRates(ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime from, DateTime to)
        {
            if (to < from)
                throw new ArgumentException("to must be later than or equal to from");

            var provider = _providerFactory.GetExchangeRateProvider(source);
            var rates = FetchRates(provider, frequency, from, to).ToList();

            if (rates.Any())
            {
                _repository.SaveRates(rates, overwriteExisting: true);
            }
            else
            {
                _logger.LogWarning("No exchange rates fetched for {source} with frequency {frequency} between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}.", source, frequency, from, to);
            }

            return rates;
        }

        public IReadOnlyList<ExchangeRateEntity> FetchAndStoreLatestRates(ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var provider = _providerFactory.GetExchangeRateProvider(source);
            var rates = FetchLatestRates(provider, frequency).ToList();

            if (rates.Any())
            {
                _repository.SaveRates(rates, overwriteExisting: true);
            }

            return rates;
        }

        private IEnumerable<ExchangeRateEntity> FetchRates(IExchangeRateProvider provider, ExchangeRateFrequencies frequency, DateTime from, DateTime to)
        {
            return frequency switch
            {
                ExchangeRateFrequencies.Daily when provider is IDailyExchangeRateProvider dailyProvider =>
                    dailyProvider.GetHistoricalDailyFxRates(from, to),
                ExchangeRateFrequencies.Monthly when provider is IMonthlyExchangeRateProvider monthlyProvider =>
                    monthlyProvider.GetHistoricalMonthlyFxRates(from, to),
                ExchangeRateFrequencies.Weekly when provider is IWeeklyExchangeRateProvider weeklyProvider =>
                    weeklyProvider.GetHistoricalWeeklyFxRates(from, to),
                ExchangeRateFrequencies.BiWeekly when provider is IBiWeeklyExchangeRateProvider biWeeklyProvider =>
                    biWeeklyProvider.GetHistoricalBiWeeklyFxRates(from, to),
                _ => throw new ExchangeRateException($"Provider {provider} does not support frequency {frequency}")
            };
        }

        private IEnumerable<ExchangeRateEntity> FetchLatestRates(IExchangeRateProvider provider, ExchangeRateFrequencies frequency)
        {
            return frequency switch
            {
                ExchangeRateFrequencies.Daily when provider is IDailyExchangeRateProvider dailyProvider =>
                    dailyProvider.GetDailyFxRates(),
                ExchangeRateFrequencies.Monthly when provider is IMonthlyExchangeRateProvider monthlyProvider =>
                    monthlyProvider.GetMonthlyFxRates(),
                ExchangeRateFrequencies.Weekly when provider is IWeeklyExchangeRateProvider weeklyProvider =>
                    weeklyProvider.GetWeeklyFxRates(),
                ExchangeRateFrequencies.BiWeekly when provider is IBiWeeklyExchangeRateProvider biWeeklyProvider =>
                    biWeeklyProvider.GetBiWeeklyFxRates(),
                _ => throw new ExchangeRateException($"Provider {provider} does not support frequency {frequency}")
            };
        }
    }
}
