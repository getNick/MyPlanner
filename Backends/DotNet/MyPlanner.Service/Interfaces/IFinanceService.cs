using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;

namespace MyPlanner.Service.Interfaces;

public interface IFinanceService
{
    // PaymentMethod operations
    Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(string userId);
    Task<PaymentMethod?> GetPaymentMethodAsync(Guid id, string userId);
    Task<Guid> CreatePaymentMethodAsync(string userId, PaymentMethod model);
    Task<bool> UpdatePaymentMethodAsync(string userId, PaymentMethod model);

    /// <summary>
    /// Deletes an unused payment method. Refused (see <see cref="PaymentMethodDeletionResult"/>) while
    /// transactions are recorded on it, in either direction, because that would orphan money.
    /// </summary>
    Task<PaymentMethodDeletionResult> DeletePaymentMethodAsync(Guid id, string userId);

    // Transaction operations (includes transaction items)
    /// <summary>
    /// Reads a bank statement for the target payment method and reports what it says — row count,
    /// date span, rows needing review — storing nothing. Refuses a target with no bank set and a
    /// file that is not a CSV statement.
    /// </summary>
    Task<BankStatementSummary> PreviewBankingFileAsync(ProcessBankingFileRequest request, string userId);

    /// <summary>
    /// Inserts a confirmed statement as Bank Transactions: new rows land with their original amount,
    /// currency and card-currency base amount; rows the ledger already has are counted, not doubled.
    /// </summary>
    Task<BankStatementImportResult> ImportBankingFileAsync(ProcessBankingFileRequest request, string userId);
    Task<ReceiptDto> PreviewReceiptAsync(ProcessReceiptRequest request);
    Task<Transaction> ConfirmReceiptAsync(ConfirmReceiptRequest request, string userId);
    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(string? userId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<Transaction?> GetTransactionAsync(Guid id, string userId);
    Task<Guid> CreateTransactionAsync(string userId, Transaction model);
    Task<bool> UpdateTransactionAsync(string userId, Transaction model);
    Task<bool> DeleteTransactionAsync(Guid id, string userId);

    // TransactionItem operations (may be accessed through transaction context)
    Task<IReadOnlyList<TransactionItem>> GetTransactionItemsAsync(Guid transactionId, string userId);
    Task<TransactionItem?> GetTransactionItemAsync(Guid id, string userId);
    Task<Guid> CreateTransactionItemAsync(string userId, TransactionItem model);
    Task<bool> UpdateTransactionItemAsync(string userId, TransactionItem model);
    Task<bool> DeleteTransactionItemAsync(Guid id, string userId);
}
