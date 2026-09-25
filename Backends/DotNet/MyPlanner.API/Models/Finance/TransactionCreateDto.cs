using System.ComponentModel.DataAnnotations;

namespace MyPlanner.API.Models.Finance;

public class TransactionCreateDto
{
    public Data.Entities.Finance.TransactionType Type { get; set; }

    [Required(ErrorMessage = "PaymentMethodId is required when making a regular transaction.")]
    public Guid? PaymentMethodId { get; set; }

    [RegularExpression("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", ErrorMessage = "ToPaymentMethodId must be a valid GUID.")]
    public Guid? ToPaymentMethodId { get; set; }

    public DateTime? Timestamp { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public required decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [StringLength(3, ErrorMessage = "Currency code must be a 3-letter ISO 4217 code.", MinimumLength = 3)]
    public string? Currency { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public required string Description { get; set; }

    [StringLength(2000, ErrorMessage = "AdditionalNotes cannot exceed 2000 characters.")]
    public string? AdditionalNotes { get; set; }

    [Range(typeof(decimal), "0", "999999999.99", ErrorMessage = "BalanceAfter must be a valid decimal value.")]
    public decimal? BalanceAfter { get; set; }

    public Data.Entities.Finance.DataOrigin DataOrigin { get; set; }
}
