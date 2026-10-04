using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Data.Entities.Finance;

public class Transaction : EntityBase
{
    public required string UserId { get; set; }
    public required TransactionType Type { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public Guid? ToPaymentMethodId { get; set; } // only for transfer
    public DateTime? Timestamp { get; set; }

    /// <summary>The amount in the Transaction Currency, absolute; the sign lives in Type.</summary>
    public required decimal Amount { get; set; }
    public required Currency Currency { get; set; }

    /// <summary>The bank's card-currency figure, not an AmountUah conversion. Null without bank evidence.</summary>
    public decimal? BaseAmount { get; set; }
    public required string Description { get; set; }
    public string? AdditionalNotes { get; set; }
    public decimal? BalanceAfter { get; set; }
    public List<TransactionItem> Items { get; set; } = new();
    public DataOrigin DataOrigin { get; set; }
    public string? RawTransactionData { get; set; }

    // A snapshot token catches stale editors without a persisted revision column. Canonical
    // decimals avoid different tokens for the same value before/after database scale conversion.
    [NotMapped]
    public string DetailVersion => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        DataOrigin, Type, Amount = CanonicalMoney(Amount), Currency, Timestamp = Timestamp?.Ticks, PaymentMethodId, ToPaymentMethodId,
        Description, AdditionalNotes,
        Items = Items.OrderBy(i => i.Id).Select(i => new
        {
            i.Id, i.Name, i.FullName, i.Category, i.Subcategory, i.Quantity,
            PricePerUnit = CanonicalMoney(i.PricePerUnit), TotalPrice = CanonicalMoney(i.TotalPrice), i.Origin
        })
    })));

    private static string CanonicalMoney(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    [NotMapped]
    public List<TransactionReviewCandidate> ReviewCandidates { get; set; } = new();

    /// <summary>
    /// Money Delta: bank amount minus Bill Line Items, derived rather than stored. Needs Items
    /// loaded. Null outside Reconciled Bills; a partial Bank/Manual breakdown is not a Money Delta.
    /// </summary>
    [NotMapped]
    public decimal? MoneyDelta => DataOrigin == DataOrigin.Reconciled
        ? Amount - Items.Sum(item => item.TotalPrice)
        : null;
}

public record TransactionReviewCandidate(Guid Id, string Description, decimal Amount, Currency Currency, DateTime? Timestamp);

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

    /// <summary>A Bill merged with its Bank Transaction: one record, bank facts and Bill detail.</summary>
    Reconciled,
}
