using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Infrastructure;
using ExchangeRate.Core.Interfaces;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core
{
    class ExchangeRateRepository : IExchangeRateRepository
    {
        private readonly IExchangeRateDataStore _dataStore;
        private readonly ILogger<ExchangeRateRepository> _logger;

        private readonly Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), Dictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>>> _ratesBySourceAndFrequency;
        private readonly Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), DateTime?> _minRateDateBySourceAndFrequency;
        private readonly Dictionary<CurrencyTypes, PeggedCurrency> _peggedCurrencies;

        public ExchangeRateRepository(IExchangeRateDataStore dataStore, ILogger<ExchangeRateRepository> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _ratesBySourceAndFrequency = new Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), Dictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>>>();
            _minRateDateBySourceAndFrequency = new Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), DateTime?>();
            _peggedCurrencies = _dataStore.GetPeggedCurrencies().ToDictionary(x => x.CurrencyId!.Value);
        }

        public IReadOnlyDictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>> GetRates(ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var key = (source, frequency);
            if (!_ratesBySourceAndFrequency.ContainsKey(key))
            {
                LoadRatesFromStore(source, frequency);
            }

            return _ratesBySourceAndFrequency[key];
        }

        public DateTime? GetMinRateDate(ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var key = (source, frequency);
            if (!_ratesBySourceAndFrequency.ContainsKey(key))
            {
                LoadRatesFromStore(source, frequency);
            }

            return _minRateDateBySourceAndFrequency.TryGetValue(key, out var minDate) ? minDate : null;
        }

        public void SaveRates(IEnumerable<ExchangeRateEntity> rates, bool overwriteExisting = true)
        {
            if (rates == null)
                return;

            var rateList = rates.Where(IsValidRate).ToList();
            if (!rateList.Any())
                return;

            _dataStore.SaveExchangeRatesAsync(rateList).GetAwaiter().GetResult();
            UpdateCache(rateList, overwriteExisting);
        }

        public IReadOnlyDictionary<CurrencyTypes, PeggedCurrency> GetPeggedCurrencies()
        {
            return _peggedCurrencies;
        }

        private void LoadRatesFromStore(ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var rates = _dataStore.ExchangeRates
                .Where(r => r.Source == source && r.Frequency == frequency)
                .ToList();

            var key = (source, frequency);
            var ratesByCurrency = new Dictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>>();
            DateTime? minDate = null;

            foreach (var rate in rates)
            {
                if (!IsValidRate(rate))
                    continue;

                var currency = rate.CurrencyId!.Value;
                var date = rate.Date!.Value.Date;
                var fxRate = rate.Rate!.Value;

                if (!ratesByCurrency.TryGetValue(currency, out var dateRates))
                {
                    dateRates = new SortedDictionary<DateTime, decimal>();
                    ratesByCurrency[currency] = dateRates;
                }

                if (!dateRates.TryGetValue(date, out var existingRate))
                {
                    dateRates[date] = fxRate;
                }
                else if (!AreRatesEqual(existingRate, fxRate))
                {
                    _logger.LogWarning("Overwriting stored exchange rate. Currency: {currency}. Date: {date:yyyy-MM-dd}. Old rate: {oldRate}. New rate: {newRate}. Source: {source}. Frequency: {frequency}", currency, date, existingRate, fxRate, source, frequency);
                    dateRates[date] = fxRate;
                }

                if (!minDate.HasValue || date < minDate.Value)
                    minDate = date;
            }

            _ratesBySourceAndFrequency[key] = ratesByCurrency;
            _minRateDateBySourceAndFrequency[key] = minDate;
        }

        private void UpdateCache(IEnumerable<ExchangeRateEntity> rates, bool overwriteExisting)
        {
            foreach (var rate in rates)
            {
                if (!IsValidRate(rate))
                    continue;

                var key = (rate.Source!.Value, rate.Frequency!.Value);
                if (!_ratesBySourceAndFrequency.TryGetValue(key, out var ratesByCurrency))
                {
                    ratesByCurrency = new Dictionary<CurrencyTypes, SortedDictionary<DateTime, decimal>>();
                    _ratesBySourceAndFrequency[key] = ratesByCurrency;
                }

                var currency = rate.CurrencyId!.Value;
                var date = rate.Date!.Value.Date;
                var fxRate = rate.Rate!.Value;

                if (!ratesByCurrency.TryGetValue(currency, out var dateRates))
                {
                    dateRates = new SortedDictionary<DateTime, decimal>();
                    ratesByCurrency[currency] = dateRates;
                }

                if (!dateRates.TryGetValue(date, out var existingRate))
                {
                    dateRates[date] = fxRate;
                }
                else if (overwriteExisting && !AreRatesEqual(existingRate, fxRate))
                {
                    _logger.LogWarning("Overwriting cached exchange rate. Currency: {currency}. Date: {date:yyyy-MM-dd}. Old rate: {oldRate}. New rate: {newRate}. Source: {source}. Frequency: {frequency}", currency, date, existingRate, fxRate, key.Item1, key.Item2);
                    dateRates[date] = fxRate;
                }

                if (!_minRateDateBySourceAndFrequency.TryGetValue(key, out var minDate) || !minDate.HasValue || date < minDate.Value)
                {
                    _minRateDateBySourceAndFrequency[key] = date;
                }
            }
        }

        private static bool IsValidRate(ExchangeRateEntity rate)
        {
            return rate.CurrencyId.HasValue &&
                   rate.Date.HasValue &&
                   rate.Source.HasValue &&
                   rate.Frequency.HasValue &&
                   rate.Rate.HasValue;
        }

        private static bool AreRatesEqual(decimal left, decimal right)
        {
            return decimal.Round(left, ExchangeRateEntity.Precision) == decimal.Round(right, ExchangeRateEntity.Precision);
        }
    }
}
