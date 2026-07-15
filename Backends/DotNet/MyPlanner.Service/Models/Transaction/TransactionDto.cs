namespace MyPlanner.Service.Models;

public record TransactionDto
{
    public DateTime? Timestamp { get; set; }
    public required decimal Amount { get; set; }

    /// <summary>
    /// What the statement says the card itself was charged, in the card's currency — for a foreign
    /// purchase, the hryvnia figure printed next to the original-currency one. Null when the source
    /// has no such column (a bill, a hand-entered transaction).
    /// </summary>
    public decimal? BaseAmount { get; set; }

    public required string Description { get; set; }
    public string? Currency { get; set; }
    public int? MCC { get; set; }
    public decimal? BalanceAfter { get; set; }
    public string? RawTransactionData { get; set; }
}

