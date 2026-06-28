namespace MyPlanner.Service.Models;

public class ReceiptDto
{
    public List<ReceiptItemDto> Items { get; set; } = new();
    public decimal? TotalAmount { get; set; }
    public string? MerchantName { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? Currency { get; set; }
    public string? PaymentMethod { get; set; }
    public string? AdditionalNotes { get; set; }
}

public class ReceiptItemDto
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public double Quantity { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public string? Barcode { get; set; }
}
