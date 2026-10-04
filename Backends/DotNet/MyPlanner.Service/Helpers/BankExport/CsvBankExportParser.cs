using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using MyPlanner.Service.Models;

namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// Reads one CSV statement with a given bank's column map. Rows it cannot read are never dropped:
/// they come back in <see cref="BankParseResult.NeedsReview"/> so the caller can list them as
/// needing review instead of letting money vanish between the file and the ledger.
/// </summary>
public class CsvBankExportParser : IBankExportParser
{
    /// <summary>Canonical columns without which no statement can be read at all.</summary>
    private static readonly string[] _requiredCanonicalColumns = ["Date", "AmountCardCurrency"];

    /// <summary>Numeric ISO 4217 codes the banks print in their currency column.</summary>
    private static readonly Dictionary<string, string> _numericIsoCodes = new()
    {
        ["980"] = "UAH",
        ["840"] = "USD",
        ["978"] = "EUR",
    };

    private static readonly Regex _threeLetterCode = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public BankParseResult Parse(Stream fileStream, BankExportProfile profile)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
        };

        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, config);

        // Read and validate header row. An empty file is not a broken statement — it is nothing.
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            return BankParseResult.Empty;

        var headers = csv.HeaderRecord.ToList();
        var columnMap = BuildColumnIndexMap(headers, profile.ColumnNameMappings);

        // A file whose header lacks the columns this bank always prints is not that bank's
        // statement, and reading it row by row would report every line as broken. Say so once.
        foreach (var canonical in _requiredCanonicalColumns)
        {
            if (!columnMap.ContainsKey(canonical))
                throw new Exceptions.UnsupportedBankStatementFileException(
                    $"This file does not look like a {profile.Name} statement: it has no " +
                    $"'{profile.ColumnNameMappings[canonical][0]}' column. Check that you exported " +
                    "a CSV from the bank, not a summary or a different report.");
        }

        var rows = new List<TransactionDto>();
        var needsReview = new List<UnreadableBankRow>();
        var recordIndex = 0;

        while (csv.Read())
        {
            // Header is line 1, so the first data record is line 2. Records spanning several
            // physical lines are counted as their position in the file, which is what a person
            // scanning the export recognises.
            var rowNumber = ++recordIndex + 1;

            var values = headers
                .Select((_, i) => i < csv.Parser.Count ? (csv.GetField(i) ?? string.Empty) : string.Empty)
                .ToList();

            TryParseRow(values, columnMap, profile, rowNumber, rows, needsReview);
        }

        // Rows newest-first, matching the bank's own export order; needs-review in file order.
        rows.Sort((a, b) => b.Timestamp?.CompareTo(a.Timestamp) ?? 0);
        needsReview.Sort((a, b) => a.RowNumber.CompareTo(b.RowNumber));

        // Which Bank Profile these rows were read as — carried out so it can be recorded alongside them.
        return new BankParseResult(rows, needsReview, profile.Name);
    }

    private static Dictionary<string, int> BuildColumnIndexMap(
        List<string> headers,
        Dictionary<string, string[]> columnNameMappings)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var trimmed = headers[i].Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            foreach (var (canonical, aliases) in columnNameMappings)
            {
                if (aliases.Any(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    map[canonical] = i;
                    break;
                }
            }
        }

        return map;
    }

    private static void TryParseRow(
        List<string> values,
        Dictionary<string, int> columnMap,
        BankExportProfile profile,
        int rowNumber,
        List<TransactionDto> rows,
        List<UnreadableBankRow> needsReview)
    {
        var description = columnMap.TryGetValue("Description", out var descriptionIdx)
            ? values[descriptionIdx].Trim()
            : string.Empty;

        // The date first: an undated row must not enter a ledger whose balance-after chain (ticket 04)
        // reads transactions in timestamp order, so it is held back and reported rather than inserted.
        var dateStr = values[columnMap["Date"]].Trim();
        if (!TryParseDate(dateStr, profile.DateFormats, out var timestamp))
        {
            needsReview.Add(new UnreadableBankRow(rowNumber, dateStr.Length == 0 ? null : dateStr, description, BankRowIssue.MissingTimestamp));
            return;
        }

        // The card-currency figure is the one a statement always carries; without it there is no
        // hryvnia amount to book, so the row is reported, never guessed at.
        var cardAmountStr = values[columnMap["AmountCardCurrency"]].Trim();
        if (!TryParseAmount(cardAmountStr, out var cardAmount))
        {
            needsReview.Add(new UnreadableBankRow(rowNumber, cardAmountStr.Length == 0 ? null : cardAmountStr, description, BankRowIssue.UnreadableAmount));
            return;
        }

        // The original-currency figure when the bank prints one (it differs on foreign purchases);
        // otherwise the purchase was in the card's currency and the two figures are the same number.
        var amount = cardAmount;
        if (columnMap.TryGetValue("AmountTransactionCurrency", out var txAmountIdx) &&
            TryParseAmount(values[txAmountIdx].Trim(), out var txAmount))
        {
            amount = txAmount;
        }

        rows.Add(new TransactionDto
        {
            Timestamp = timestamp,
            Amount = amount,
            BaseAmount = cardAmount,
            Description = description,
            Currency = ReadCurrency(values, columnMap, profile),
            MCC = ReadOptionalInt(values, columnMap, "MCC"),
            BalanceAfter = ReadOptionalDecimal(values, columnMap, "BalanceAfter"),
            // Where in the file this row came from. The verbatim line itself is no longer carried here:
            // the whole Statement File is stored now, and a line number is how it gets read again (D5).
            RowNumber = rowNumber,
        });
    }

    private static bool TryParseDate(string dateStr, string[] formats, out DateTime timestamp)
    {
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                timestamp = parsed;
                return true;
            }
        }

        timestamp = default;
        return false;
    }

    private static bool TryParseAmount(string amountStr, out decimal amount) =>
        decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);

    /// <summary>
    /// The transaction's currency as an ISO code the caller can act on: letter codes uppercased,
    /// numeric ISO codes translated, the banks' "EURO" spelling normalised. An unrecognised cell
    /// comes back null so the caller falls back to the card's currency — it is never silently
    /// called UAH, because a pound spent in London booked as hryvnia loses money twice over.
    /// </summary>
    private static string? ReadCurrency(List<string> values, Dictionary<string, int> columnMap, BankExportProfile profile)
    {
        var raw = columnMap.TryGetValue("Currency", out var idx) ? values[idx].Trim() : string.Empty;

        if (raw.Length == 0 || raw == "—")
            return profile.DefaultCurrency;

        if (_numericIsoCodes.TryGetValue(raw, out var numeric))
            return numeric;

        var upper = raw.ToUpperInvariant();
        if (upper == "EURO")
            return "EUR";
        if (_threeLetterCode.IsMatch(upper))
            return upper;

        return null;
    }

    private static int? ReadOptionalInt(List<string> values, Dictionary<string, int> columnMap, string canonical)
    {
        if (!columnMap.TryGetValue(canonical, out var idx))
            return null;

        return int.TryParse(values[idx].Trim(), out var parsed) ? parsed : null;
    }

    private static decimal? ReadOptionalDecimal(List<string> values, Dictionary<string, int> columnMap, string canonical)
    {
        if (!columnMap.TryGetValue(canonical, out var idx))
            return null;

        var raw = values[idx].Trim();
        if (raw.Length == 0 || raw == "—")
            return null;

        return TryParseAmount(raw, out var parsed) ? parsed : null;
    }
}
