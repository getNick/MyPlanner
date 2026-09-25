using System.ComponentModel.DataAnnotations;
using MyPlanner.Data.Entities.Finance;

namespace MyPlanner.API.Models.Finance;

public class TransactionItemCreateDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public required string Name { get; set; }

    [StringLength(500, ErrorMessage = "FullName cannot exceed 500 characters.")]
    public string? FullName { get; set; }

    [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
    public string? Category { get; set; }

    [StringLength(100, ErrorMessage = "Subcategory cannot exceed 100 characters.")]
    public string? Subcategory { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public double Quantity { get; set; } = 1;

    [Required(ErrorMessage = "PricePerUnit is required.")]
    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "PricePerUnit must be a non-negative value.")]
    public decimal PricePerUnit { get; set; }

    [Required(ErrorMessage = "TotalPrice is required.")]
    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "TotalPrice must be a non-negative value.")]
    public decimal TotalPrice { get; set; }

    public ItemOrigin? Origin { get; set; }
}
