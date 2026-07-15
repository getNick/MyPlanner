namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// Reads one statement format — CSV today — with a given bank's column map.
/// Implementations never drop a row they could not read: it comes back in <see cref="BankParseResult.NeedsReview"/>.
/// </summary>
public interface IBankExportParser
{
    BankParseResult Parse(Stream fileStream, BankExportProfile profile);
}
