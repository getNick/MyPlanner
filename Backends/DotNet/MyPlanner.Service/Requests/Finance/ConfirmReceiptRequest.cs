using MyPlanner.Service.Models;

namespace MyPlanner.Service.Requests.Finance;

/// <summary>Corrected Bill Draft fields submitted with the original receipt image.</summary>
public sealed class ConfirmReceiptRequest : UploadFileBaseRequest
{
    public required ReceiptDto Receipt { get; init; }
    public Guid? PaymentMethodId { get; init; }
}
