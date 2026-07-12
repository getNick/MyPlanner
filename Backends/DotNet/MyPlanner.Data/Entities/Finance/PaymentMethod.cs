using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Data.Entities.Finance;

public class PaymentMethod : EntityBase
{
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required PaymentMethodType Type { get; set; }
    public required Currency Currency { get; set; }
    public string? BankProvider { get; set; }
}

public enum PaymentMethodType
{
    BankCard,
    Cash,
    SavingsAccount,
    Other,
}

public enum Currency
{
    UAH,
    USD,
    EURO,
}
