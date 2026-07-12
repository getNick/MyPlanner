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
}
