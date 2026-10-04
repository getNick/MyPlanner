using System.ComponentModel.DataAnnotations;
using MyPlanner.Data.Entities.Finance;

namespace MyPlanner.API.Models.Finance;

/// <summary>Update contract is independent of creation: Bills may have an Unknown Payment Method
/// and zero Total. Bank facts on a Reconciled Bill are preserved by the service.</summary>
public class TransactionUpdateDto
{
    public Guid Id { get; set; }
    public TransactionType Type { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public Guid? ToPaymentMethodId { get; set; }
    public DateTime? Timestamp { get; set; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal Amount { get; set; }

    [Required, StringLength(4, MinimumLength = 3)]
    public string? Currency { get; set; }

    [Required, StringLength(500)]
    public required string Description { get; set; }

    [StringLength(2000)]
    public string? AdditionalNotes { get; set; }

    public decimal? BalanceAfter { get; set; }
    public DataOrigin DataOrigin { get; set; }

    /// <summary>Supplied means a complete Expense detail save; [] removes all detail. Omitted retains legacy header-only behaviour.</summary>
    public List<BillLineItemSaveDto>? Items { get; set; }
    public string? ExpectedDetailVersion { get; set; }
}
