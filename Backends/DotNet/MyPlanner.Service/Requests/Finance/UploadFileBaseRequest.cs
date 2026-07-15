namespace MyPlanner.Service.Requests.Finance;

public class UploadFileBaseRequest 
{
    public Stream FileStream { get; set; } = default!;
    public string ContentType { get; set; } = default!;

    public UploadFileBaseRequest() { }
    public UploadFileBaseRequest(Stream fileStream, string contentType)
    {
        FileStream = fileStream;
        ContentType = contentType;
    }
}
