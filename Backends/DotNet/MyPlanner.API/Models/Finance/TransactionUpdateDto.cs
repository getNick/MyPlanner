namespace MyPlanner.API.Models.Finance;

public class TransactionUpdateDto : TransactionCreateDto
{
    public Guid Id { get; set; }
}
