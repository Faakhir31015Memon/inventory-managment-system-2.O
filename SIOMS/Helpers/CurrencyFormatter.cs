namespace SIOMS.Helpers
{
    // ✅ ADDED: single source of truth for how money is displayed across the whole
    // app (Products, Sales, Purchases, Dashboard, Reports). Views call
    // CurrencyFormatter.Format(amount) instead of amount.ToString("C") (which was
    // hardcoded to the server's culture, always showing "$" regardless of the
    // Currency selected in Settings).
    //
    // Symbol is set once at startup (Program.cs, from the saved Settings row) and
    // refreshed immediately whenever Settings are saved (SettingsController), so a
    // currency change takes effect on every page without an app restart.
    public static class CurrencyFormatter
    {
        // ✅ Phase 2 - Multi-Currency: supported currency codes for Product's
        // OriginalCurrency and (later) the system-wide display currency.
        public static readonly string[] SupportedCurrencies = { "PKR", "USD", "EUR", "GBP", "INR", "AED" };

        public static string Symbol { get; set; } = "Rs.";

        public static string Format(decimal amount)
        {
            return $"{Symbol} {amount:N2}";
        }
    }
}
