using MyPlanner.Service.Models;

namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// Why a statement row could not be turned into a transaction. Every case is a row the import
/// refuses to guess about, never one it drops in silence.
/// </summary>
public enum BankRowIssue
{
    /// <summary>The date cell is empty or in a format this bank's profile does not know.</summary>
    MissingTimestamp,

    /// <summary>The card-currency amount cell is empty or not a number.</summary>
    UnreadableAmount,
}

/// <summary>
/// One statement row the parser could not read, reported so the caller can list it as needing
/// review. Inserting it anyway would put an undated row into a ledger whose balance-after chain
/// (ticket 04) reads timestamps in order.
/// </summary>
/// <param name="RowNumber">Line number in the CSV file, counting the header as line 1.</param>
/// <param name="RawValue">The offending cell — the date or amount exactly as the bank printed it.</param>
/// <param name="Description">The row's description, so the user can recognise which purchase it is.</param>
/// <param name="Issue">Which read failed.</param>
public sealed record UnreadableBankRow(
    int RowNumber,
    string? RawValue,
    string? Description,
    BankRowIssue Issue);

/// <summary>
/// What one statement file yielded: the rows worth inserting, and the rows that need a person.
/// Nothing read from the file is lost between the two lists.
/// </summary>
/// <param name="ProfileName">Which <strong>Bank Profile</strong> the file was read as — the assumption a
/// future re-parse needs in order to reproduce these same rows (D8).</param>
public sealed record BankParseResult(
    IReadOnlyList<TransactionDto> Rows,
    IReadOnlyList<UnreadableBankRow> NeedsReview,
    string? ProfileName = null)
{
    public static BankParseResult Empty { get; } = new(Array.Empty<TransactionDto>(), Array.Empty<UnreadableBankRow>());
}
