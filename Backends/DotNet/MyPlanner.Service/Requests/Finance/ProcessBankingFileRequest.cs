namespace MyPlanner.Service.Requests.Finance;

public class ProcessBankingFileRequest : UploadFileBaseRequest
{
    /// <summary>
    /// Payment method ID used to resolve the bank provider and currency.
    /// </summary>
    public Guid PaymentMethodId { get; set; }
}
