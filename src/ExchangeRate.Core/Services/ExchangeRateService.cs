using System;
using System.Linq;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Helpers;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace ExchangeRate.Core.Services
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly IExchangeRateRepository _repository;
        private readonly IExchangeRateProviderFactory _providerFactory;
        private readonly IExchangeRateFetcher _fetcher;
        private readonly IExchangeRateConversionService _conversionService;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(
            IExchangeRateRepository repository,
            IExchangeRateProviderFactory providerFactory,
            IExchangeRateFetcher fetcher,
            IExchangeRateConversionService conversionService,
            ILogger<ExchangeRateService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
            _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public decimal? GetRate(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            if (fromCurrency == toCurrency)
                return 1m;

            date = date.Date;
            var provider = _providerFactory.GetExchangeRateProvider(source);

            EnsureRatesLoaded(date, source, frequency);

            var rates = _repository.GetRates(source, frequency);
            var minFxDate = _repository.GetMinRateDate(source, frequency) ?? DateTime.MaxValue;
            var peggedCurrencies = _repository.GetPeggedCurrencies();

            var result = _conversionService.TryGetRate(rates, peggedCurrencies, date, minFxDate, provider, fromCurrency, toCurrency);
            if (result.IsSuccess)
                return result.Value;

            if (result.Errors.FirstOrDefault() is NoFxRateFoundError)
            {
                FetchMissingRates(date, source, frequency, minFxDate);

                rates = _repository.GetRates(source, frequency);
                minFxDate = _repository.GetMinRateDate(source, frequency) ?? DateTime.MaxValue;

                result = _conversionService.TryGetRate(rates, peggedCurrencies, date, minFxDate, provider, fromCurrency, toCurrency);
                if (result.IsSuccess)
                    return result.Value;
            }

            var missingCurrency = result.Errors.OfType<NotSupportedCurrencyError>().FirstOrDefault()?.Currency;
            var lookupCurrency = missingCurrency ?? (toCurrency == provider.Currency ? fromCurrency : toCurrency);

            _logger.LogError("No {source} {frequency} exchange rate found for {lookupCurrency} on {date:yyyy-MM-dd}. Earliest available date: {minFxDate:yyyy-MM-dd}. FromCurrency: {fromCurrency}, ToCurrency: {toCurrency}",
                source, frequency, lookupCurrency, date, minFxDate == DateTime.MaxValue ? DateTime.MinValue : minFxDate, fromCurrency, toCurrency);

            return null;
        }

        public decimal? GetRate(string fromCurrencyCode, string toCurrencyCode, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var fromCurrency = CurrencyCodeMapper.ParseCurrencyCode(fromCurrencyCode);
            var toCurrency = CurrencyCodeMapper.ParseCurrencyCode(toCurrencyCode);

            return GetRate(fromCurrency, toCurrency, date, source, frequency);
        }

        private void EnsureRatesLoaded(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var existingRates = _repository.GetRates(source, frequency);
            if (existingRates.Count > 0)
                return;

            var seedDate = DateTime.UtcNow.Date;
            var rangeStart = PeriodHelper.GetStartOfMonth(date.AddMonths(-1));
            var rangeEnd = seedDate > date ? seedDate : date;

            _fetcher.FetchAndStoreRates(source, frequency, rangeStart, rangeEnd);
        }

        private void FetchMissingRates(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime minFxDate)
        {
            var rangeStart = minFxDate == DateTime.MaxValue
                ? PeriodHelper.GetStartOfMonth(date.AddMonths(-1))
                : minFxDate;

            var rangeEnd = date;

            if (minFxDate == DateTime.MaxValue)
            {
                var seedDate = DateTime.UtcNow.Date;
                rangeEnd = seedDate > date ? seedDate : date;
            }
            else if (minFxDate > date)
            {
                rangeStart = PeriodHelper.GetStartOfMonth(date.AddMonths(-1));
                rangeEnd = minFxDate;
            }

            _fetcher.FetchAndStoreRates(source, frequency, rangeStart, rangeEnd);
        }
    }
}
