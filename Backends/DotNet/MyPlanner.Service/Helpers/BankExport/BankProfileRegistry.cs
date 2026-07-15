using MyPlanner.Service.Exceptions;

namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// Registry of bank export parsing profiles.
/// Add new banks here by creating a new <see cref="BankExportProfile"/> entry.
/// </summary>
public static class BankProfileRegistry
{
    private static readonly Dictionary<string, BankExportProfile> _profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monobank"] = new(
            Name: "MonoBank",

            ColumnNameMappings: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Date"]       = ["Дата i час операції", "Дата і час операції", "Date", "Time"],
                ["Description"]= ["Деталі операції", "Деталі", "Опис", "Description", "Details", "Назва"],
                ["MCC"]        = ["MCC", "Код MCC"],
                ["AmountCardCurrency"]       = ["Сума в валюті картки (UAH)", "Сума в валюті картки", "Amount in Card Currency", "Сума"],
                ["AmountTransactionCurrency"]= ["Сума в валюті операції", "Сума в валюті транзакції", "Amount in Transaction Currency"],
                ["Currency"]     = ["Валюта", "Currency", "Валюта операції"],
                ["BalanceAfter"] = ["Залишок після операції", "Залишок", "Balance After", "Поточний баланс"],
                ["Fees"]         = ["Сума комісій (UAH)", "Комісія", "Fees", "Сума комісії"],
                ["Cashback"]     = ["Сума кешбеку (UAH)", "Кешбек", "Cashback", "Сума кешбеку"],
            },

            DateFormats: new[] { "dd.MM.yyyy HH:mm:ss" },

            DefaultCurrency: "UAH")
    };

    /// <summary>Display names of the banks whose statement formats we can read.</summary>
    private static string SupportedBanks =>
        string.Join(", ", _profiles.Values.Select(p => p.Name));

    /// <summary>
    /// Gets the bank export profile for the given provider name. There is no default: reading a
    /// statement with another bank's columns invents dates and amounts, so an unknown or missing
    /// provider is refused rather than guessed past.
    /// </summary>
    /// <param name="provider">Bank/provider name (e.g., "Monobank"). Case-insensitive.</param>
    /// <exception cref="UnsupportedBankProviderException">No profile exists for this provider.</exception>
    public static BankExportProfile GetProfile(string provider)
    {
        if (_profiles.TryGetValue(provider ?? string.Empty, out var profile))
            return profile;

        // A blank provider never reaches the fuzzy match: an empty string is contained in every
        // key, so "no bank set" would silently borrow the first bank's columns.
        if (string.IsNullOrWhiteSpace(provider))
            throw NoProfile(provider);

        // Try to find a partial match (e.g., "mono" -> "MonoBank")
        var needle = provider;
        var matched = _profiles.FirstOrDefault(p => p.Key.Contains(needle, StringComparison.OrdinalIgnoreCase)
                                               || needle.Contains(p.Key, StringComparison.OrdinalIgnoreCase));

        if (matched.Value != null)
            return matched.Value;

        throw NoProfile(provider);
    }

    private static UnsupportedBankProviderException NoProfile(string? provider) =>
        string.IsNullOrWhiteSpace(provider)
            ? new UnsupportedBankProviderException(
                $"No bank is set on this payment method, so there is no statement format to read. Supported banks: {SupportedBanks}.")
            : new UnsupportedBankProviderException(
                $"Cannot read '{provider}' statements — no column map for that bank. Supported banks: {SupportedBanks}.");

    /// <summary>
    /// Returns a list of all registered provider names.
    /// </summary>
    public static IReadOnlyCollection<string> AvailableProviders => _profiles.Keys;
}
