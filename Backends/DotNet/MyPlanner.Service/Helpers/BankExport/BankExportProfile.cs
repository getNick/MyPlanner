namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// Defines how to parse a bank export from a specific provider.
/// Contains column name mappings (Ukrainian + English) and date formats.
/// </summary>
public record BankExportProfile(
    /// <summary>
    /// Human-readable bank/provider name (e.g., "MonoBank", "PrivatBank").
    /// </summary>
    string Name,

    /// <summary>
    /// Maps canonical field names to arrays of possible column header strings.
    /// Keys: "Date", "Description", "AmountCardCurrency", "AmountTransactionCurrency",
    ///       "Currency", "MCC", "BalanceAfter", "Fees", "Cashback".
    /// Values: [Ukrainian, English, ...] — order doesn't matter.
    /// </summary>
    Dictionary<string, string[]> ColumnNameMappings,

    /// <summary>
    /// Date/time formats the bank uses (tried in order).
    /// Uses .NET custom date/time format strings.
    /// </summary>
    string[] DateFormats,

    /// <summary>
    /// Default currency code (ISO 4217) when no currency column is present or is empty.
    /// </summary>
    string DefaultCurrency = "UAH"
);
