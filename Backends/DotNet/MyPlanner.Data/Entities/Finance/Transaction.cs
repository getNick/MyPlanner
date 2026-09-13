using System.ComponentModel.DataAnnotations.Schema;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Data.Entities.Finance;

public class Transaction : EntityBase
{
    public required string UserId { get; set; }
    public required TransactionType Type { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public Guid? ToPaymentMethodId { get; set; } // only for transfer

    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// The amount in the transaction's own currency (<see cref="Currency"/>), absolute value — the
    /// sign lives in <see cref="Type"/>, which is also the row's money role.
    /// </summary>
    public required decimal Amount { get; set; }
    public required Currency Currency { get; set; }

    /// <summary>
    /// What the move cost in the card's currency, taken straight from the statement's
    /// "Сума в валюті картки (UAH)" column. Null on rows no bank statement produced — a Bill typed
    /// at the kitchen table has no card-currency figure to borrow. A foreign-currency purchase keeps
    /// <see cref="Amount"/> in what it was actually paid in and reports this alongside it.
    /// </summary>
    public decimal? BaseAmount { get; set; }
    public required string Description { get; set; }
    public string? AdditionalNotes { get; set; }
    public decimal? BalanceAfter { get; set; }

    public List<TransactionItem> Items { get; set; } = new();
    public DataOrigin DataOrigin { get; set; }
    public string? RawTransactionData { get; set; }

    /// <summary>
    /// <strong>Money Delta</strong>: the bank amount minus the sum of the Bill's Line Items — how much
    /// of the purchase the paper failed to explain. Never stored: it is always derived from the two
    /// things it compares, so editing Line Items widens it without any write, and the recorded bank
    /// amount is untouched. Null for anything that is not a Reconciled row — a Provisional Bill has
    /// no bank total to differ from. Reading it needs Line Items loaded, so the queries
    /// <c>Include(t =&gt; t.Items)</c>.
    /// </summary>
    [NotMapped]
    public decimal? MoneyDelta => DataOrigin == DataOrigin.Reconciled
        ? Amount - Items.Sum(item => item.TotalPrice)
        : null;
}

public enum TransactionType
{
    Expense,
    Income,
    Transfer, // Between own accounts
}

public enum DataOrigin
{
    Bank,
    Manual,
    Receipt,

    /// <summary>
    /// A Bill that absorbed the Bank Transaction it matches: the bank figures are authoritative, the
    /// Bill's Line Items survive. One purchase, one row.
    /// </summary>
    Reconciled,
}
