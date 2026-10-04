using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Models;

namespace MyPlanner.Service;

/// <summary>
/// Automatic <strong>Matching</strong> at the complete-write seam. A Bill and its Bank Transaction
/// merge into one Reconciled row — the bank's money facts and the Bill's Line Items.
/// </summary>
public partial class FinanceService
{
    /// <summary>The matching window (±1 hour). Deliberately strict: a wrong pairing is worse than a
    /// Bill left Provisional, because the user stops being able to trust the ledger.</summary>
    private static readonly TimeSpan MatchingWindow = TimeSpan.FromHours(1);
    private const decimal MatchingAmountTolerance = 0.10m;

    /// <summary>
    /// Only touched pairs may merge, but uniqueness is assessed across the household.
    /// Returns absorbed Bank Transaction IDs mapped to the surviving Bill IDs.
    /// The caller owns persistence and the transaction enclosing ingestion and Matching.
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> MatchTransactionsAsync(string userId, IReadOnlyCollection<Guid> touchedIds)
    {
        if (touchedIds.Count == 0) return new();
        var bills = await _context.Transactions
            .Include(t => t.Items)
            .Where(t => t.UserId == userId
                        && t.DataOrigin == DataOrigin.Receipt
                        && t.Type == TransactionType.Expense
                        && t.Timestamp != null)
            .ToListAsync();

        var bankRows = await _context.Transactions
            .Include(t => t.Items)
            .Where(t => t.UserId == userId
                        && t.DataOrigin == DataOrigin.Bank
                        && t.Type == TransactionType.Expense
                        && t.Timestamp != null)
            .ToListAsync();

        var pairs = bills
            .SelectMany(bill => bankRows
                .Where(bankRow => IsMatch(bill, bankRow))
                .Select(bankRow => (Bill: bill, BankRow: bankRow)))
            .ToList();

        // Exactly one candidate pair, or nothing. Ambiguous candidates remain untouched.
        var ambiguous = pairs
            .GroupBy(p => p.Bill.Id).Where(g => g.Count() > 1).Select(g => g.Key)
            .Concat(pairs.GroupBy(p => p.BankRow.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            .ToHashSet();

        var toMerge = pairs
            .Where(p => (touchedIds.Contains(p.Bill.Id) || touchedIds.Contains(p.BankRow.Id))
                        && !ambiguous.Contains(p.Bill.Id) && !ambiguous.Contains(p.BankRow.Id))
            .OrderBy(p => p.Bill.Timestamp!.Value)
            .ThenBy(p => p.Bill.Id)
            .ToList();

        foreach (var (bill, bankRow) in toMerge) MergeBillWithBankRow(bill, bankRow);

        return toMerge.ToDictionary(p => p.BankRow.Id, p => p.Bill.Id);
    }

    /// <summary>Whether a Bill and a statement row are the same purchase: same currency, the exact
    /// same amount, within the matching window. A Bill with no amount is an empty OCR result, never
    /// evidence of a purchase.</summary>
    private static bool IsMatch(Transaction bill, Transaction bankRow) =>
        bill.Currency == bankRow.Currency
        && Math.Abs(bill.Amount - bankRow.Amount) <= MatchingAmountTolerance
        && bill.Amount != 0
        && bill.Timestamp != null && bankRow.Timestamp != null
        && (bankRow.Timestamp.Value - bill.Timestamp.Value).Duration() <= MatchingWindow;

    /// <summary>
    /// Merge: the Bill row keeps its identity, the Bank Transaction's facts are copied onto it, and
    /// the statement row is removed — one purchase, one ledger row. Line Items are not touched: the
    /// Bill is the detail-bearing record, and what the paper failed to explain becomes Money Delta.
    /// </summary>
    private void MergeBillWithBankRow(Transaction bill, Transaction bankRow)
    {
        bill.Timestamp = bankRow.Timestamp;   // so a re-import of the same file stays a duplicate
        bill.Description = bankRow.Description; // Keep the bank's statement description, not the receipt fallback/merchant.
        bill.Amount = bankRow.Amount;         // the bank amount is the ledger number
        bill.Currency = bankRow.Currency;
        bill.BaseAmount = bankRow.BaseAmount;
        bill.BalanceAfter = bankRow.BalanceAfter;
        bill.PaymentMethodId = bankRow.PaymentMethodId; // Owner inherited from the card that matched
        // A Reconciled row has both sources, so its envelope keeps both sides: the Bill's image pointer
        // survives and the Bank side — FileKey, line, profile — travels with it. Overwriting one with the
        // other would throw away half of what this merge is evidence of.
        bill.RawTransactionData = RawTransactionDataEnvelope.Read(bill.RawTransactionData)
            .WithBank(RawTransactionDataEnvelope.Read(bankRow.RawTransactionData).Bank)
            .ToJson();
        bill.DataOrigin = DataOrigin.Reconciled;

        _context.Transactions.Remove(bankRow);
    }
}
