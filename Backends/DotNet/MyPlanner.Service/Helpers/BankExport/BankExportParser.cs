using MyPlanner.Service.Exceptions;

namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// Entry point for reading a bank statement export. The two things it decides are the ones a caller
/// must never decide for itself: what kind of file this is, and which bank's columns to read it with.
/// Both answers are refusals rather than guesses — see <see cref="UnsupportedBankStatementFileException"/>
/// and <see cref="UnsupportedBankProviderException"/>.
/// </summary>
public static class BankExportParser
{
    private static readonly IBankExportParser _csvParser = new CsvBankExportParser();

    /// <summary>Declared content types that mean "text CSV". A declared PDF, image or spreadsheet is refused.</summary>
    private static readonly HashSet<string> _csvContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv",
        "application/csv",
        "text/plain", // some browsers label a .csv this way
        "application/vnd.ms-excel", // some banks export CSV under this type
    };

    /// <summary>
    /// Reads one statement file for the bank the target payment method declares.
    /// </summary>
    /// <param name="fileStream">The uploaded file.</param>
    /// <param name="contentType">What the uploader says the file is; used only to refuse non-CSV files.</param>
    /// <param name="bankProvider">The target payment method's <c>BankProvider</c>; picks the column map.</param>
    /// <returns>The rows worth inserting, and the rows a person needs to look at.</returns>
    public static BankParseResult Parse(Stream fileStream, string contentType, string bankProvider)
    {
        if (!IsCsvContentType(contentType))
            throw new UnsupportedBankStatementFileException(
                $"'{contentType}' is not a CSV statement. Statements are read from the bank's CSV export only; " +
                "a PDF or a photo of one is not accepted here.");

        // No profile means no column map, so there is nothing to read the file with — hence no fallback.
        var profile = BankProfileRegistry.GetProfile(bankProvider);

        return _csvParser.Parse(fileStream, profile);
    }

    /// <summary>
    /// Whether the declared content type may be read as CSV. An empty or <c>application/octet-stream</c>
    /// declaration means the uploader never said what the file is, so the only way to find out is to read
    /// it as text — anything that then fails to line up with the bank's columns comes back as needing review.
    /// </summary>
    internal static bool IsCsvContentType(string? contentType)
    {
        var declared = (contentType ?? string.Empty).Split(';')[0].Trim();

        return declared.Length == 0
            || declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || _csvContentTypes.Contains(declared);
    }
}
