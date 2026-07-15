namespace MyPlanner.Service.Models;

/// <summary>
/// Why a payment-method deletion request ended the way it did.
/// </summary>
public enum PaymentMethodDeletionStatus
{
    /// <summary>The payment method had no transactions and is gone.</summary>
    Deleted,

    /// <summary>No payment method with that id belongs to the requesting household.</summary>
    NotFound,

    /// <summary>
    /// Transactions were recorded on the payment method, so deleting it would orphan money.
    /// The caller has to remove (or re-attribute) those transactions first.
    /// </summary>
    InUse,
}

/// <summary>
/// Outcome of <c>DeletePaymentMethodAsync</c>. Deletion is refused rather than cascading:
/// a payment method with transactions is never removed silently, and the caller is told how
/// many transactions are in the way so it can explain the refusal to a person.
/// </summary>
public record PaymentMethodDeletionResult(PaymentMethodDeletionStatus Status, int TransactionCount)
{
    public bool Deleted => Status == PaymentMethodDeletionStatus.Deleted;

    /// <summary>
    /// Server-side explanation of a refusal. Clients may render their own copy, but the facts
    /// (and the count behind them) come from here so no client invents its own arithmetic.
    /// </summary>
    public string Explanation => Status switch
    {
        PaymentMethodDeletionStatus.InUse =>
            $"{TransactionCount} transaction{(TransactionCount == 1 ? "" : "s")} recorded on this payment method. Delete or re-attribute them first.",
        PaymentMethodDeletionStatus.NotFound => "Payment method not found.",
        _ => "Payment method deleted.",
    };
}
