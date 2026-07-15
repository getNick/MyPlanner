using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Helpers.BankExport;

namespace MyPlanner.Service.Models;

/// <summary>
/// One statement row that could not be read, phrased for a person rather than for the parser:
/// which line of the file, what the bank printed in it, and why it is not bookable.
/// </summary>
public record BankStatementReviewRow(
    int RowNumber,
    string? RawValue,
    string? Description,
    string Reason)
{
    public static BankStatementReviewRow From(UnreadableBankRow row) => new(
        row.RowNumber,
        row.RawValue,
        row.Description,
        row.Issue switch
        {
            BankRowIssue.MissingTimestamp => "No readable date — the row cannot be placed in time, so it was not imported.",
            BankRowIssue.UnreadableAmount => "No readable amount — the sum the card was charged is missing or not a number.",
            _ => "The row could not be read.",
        });
}

/// <summary>
/// What one statement file says, before anything is written down: how many rows it holds, the span
/// they cover, and the rows that need a person. The preview and the import echo the same summary,
/// so what the user confirmed is what was read.
/// </summary>
public record BankStatementSummary(
    Guid PaymentMethodId,
    string PaymentMethodName,
    string BankProvider,
    int RowCount,
    DateTime? FirstTimestamp,
    DateTime? LastTimestamp,
    IReadOnlyList<BankStatementReviewRow> NeedsReview);

/// <summary>
/// The outcome of a confirmed import: what the file said (<see cref="Summary"/>), how much of it
/// was new, how much the ledger already had, and the transactions as stored — so the caller can
/// show them without a second read.
/// </summary>
public record BankStatementImportResult(
    BankStatementSummary Summary,
    int InsertedRowCount,
    int DuplicateRowCount,
    IReadOnlyList<Transaction> Transactions);
