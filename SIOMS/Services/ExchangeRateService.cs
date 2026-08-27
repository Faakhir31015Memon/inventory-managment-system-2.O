using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Helpers;

namespace SIOMS.Services
{
    /// <summary>
    /// Phase 4 - Live Exchange Rate API.
    ///
    /// Fetches live exchange rates (relative to USD) from open.er-api.com — a free,
    /// no-API-key-required endpoint updated daily and covering 160+ currencies
    /// including all 6 currencies SIOMS supports (PKR, USD, EUR, GBP, INR, AED).
    ///
    /// Rates are cached in the Settings table. If the API call fails for any reason
    /// (no internet, API down, timeout, unexpected response), the last cached rates
    /// are used instead. If there is no cache at all yet (very first run with no
    /// internet), a safe 1:1 fallback is used so the app never crashes and no
    /// transaction is ever blocked because of this service.
    /// </summary>
    public class ExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        private const string ApiUrl = "https://open.er-api.com/v6/latest/USD";

        // How long a cached set of rates is considered "fresh" before we try
        // hitting the API again. open.er-api.com itself only updates once a day.
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);

        public ExchangeRateService(HttpClient httpClient, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        /// <summary>
        /// Returns currency rates relative to USD (1 USD = rate[code] units of that currency).
        /// Tries the live API first (if cache is stale/missing), then falls back to the
        /// last cached rates, then finally to a hardcoded 1:1 fallback.
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetRatesAsync()
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();

            bool cacheIsStale = settings?.ExchangeRatesUpdatedAt == null ||
                                 DateTime.Now - settings.ExchangeRatesUpdatedAt.Value > CacheLifetime ||
                                 string.IsNullOrEmpty(settings.CachedExchangeRatesJson);

            if (cacheIsStale)
            {
                var freshRates = await TryFetchFromApiAsync();
                if (freshRates != null)
                {
                    if (settings != null)
                    {
                        settings.CachedExchangeRatesJson = JsonSerializer.Serialize(freshRates);
                        settings.ExchangeRatesUpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                    return freshRates;
                }
                // API failed — fall through and use whatever is cached below.
            }

            if (!string.IsNullOrEmpty(settings?.CachedExchangeRatesJson))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<Dictionary<string, decimal>>(settings.CachedExchangeRatesJson);
                    if (cached != null && cached.Count > 0)
                    {
                        return cached;
                    }
                }
                catch
                {
                    // Corrupted cache value — ignore and fall through to hardcoded fallback.
                }
            }

            // Nothing usable: brand new install with no internet on first run.
            // Return 1:1 so the app keeps working (no conversion applied) instead of crashing.
            return CurrencyFormatter.SupportedCurrencies.ToDictionary(c => c, c => 1m);
        }

        /// <summary>
        /// Converts an amount from one currency to another using an already-fetched
        /// rates dictionary (from GetRatesAsync). Use this in loops (e.g. a product
        /// list) so rates are fetched once, not once per item.
        /// </summary>
        public static decimal Convert(decimal amount, string fromCurrency, string toCurrency, Dictionary<string, decimal> rates)
        {
            if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency) ||
                string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return amount;
            }

            decimal fromRate = rates.TryGetValue(fromCurrency, out var f) && f > 0 ? f : 1m;
            decimal toRate = rates.TryGetValue(toCurrency, out var t) ? t : 1m;

            // amount (fromCurrency) -> USD -> toCurrency
            var amountInUsd = amount / fromRate;
            return Math.Round(amountInUsd * toRate, 2);
        }

        /// <summary>
        /// Converts an amount from one currency to another using cached/live rates.
        /// Never throws — worst case it returns the original amount unconverted.
        /// Prefer GetRatesAsync() + Convert() when converting many amounts at once.
        /// </summary>
        public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency)
        {
            if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency) ||
                string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return amount;
            }

            var rates = await GetRatesAsync();
            return Convert(amount, fromCurrency, toCurrency, rates);
        }

        private async Task<Dictionary<string, decimal>?> TryFetchFromApiAsync()
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(8);
                var response = await _httpClient.GetAsync(ApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("result", out var resultProp) ||
                    resultProp.GetString() != "success")
                {
                    return null;
                }

                if (!doc.RootElement.TryGetProperty("rates", out var ratesProp))
                {
                    return null;
                }

                var rates = new Dictionary<string, decimal>();
                foreach (var currency in CurrencyFormatter.SupportedCurrencies)
                {
                    if (ratesProp.TryGetProperty(currency, out var rateVal) &&
                        rateVal.TryGetDecimal(out var rateDecimal))
                    {
                        rates[currency] = rateDecimal;
                    }
                }

                // Only trust the result if every currency we need came back.
                return rates.Count == CurrencyFormatter.SupportedCurrencies.Length ? rates : null;
            }
            catch
            {
                // Network failure, timeout, malformed JSON, etc. — never throw.
                // Callers fall back to cached/hardcoded rates instead.
                return null;
            }
        }
    }
}
