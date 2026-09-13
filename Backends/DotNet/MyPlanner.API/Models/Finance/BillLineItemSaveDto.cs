using System.ComponentModel.DataAnnotations;

namespace MyPlanner.API.Models.Finance;

/// <summary>Complete Bill detail: omit Id for a new Line Item; an existing Id must belong to the Bill.</summary>
public class BillLineItemSaveDto
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public required string Name { get; set; }

    [StringLength(500)]
    public string? FullName { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? Subcategory { get; set; }

    [Range(0, double.MaxValue)]
    public double Quantity { get; set; } = 1;

    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal PricePerUnit { get; set; }

    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal TotalPrice { get; set; }
}
