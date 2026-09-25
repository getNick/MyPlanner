namespace MyPlanner.API.Models.Finance;

public class PaymentMethodUpdateDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Data.Entities.Finance.PaymentMethodType Type { get; set; }
    public Data.Entities.Finance.Currency Currency { get; set; }
    public string? BankProvider { get; set; }
}
