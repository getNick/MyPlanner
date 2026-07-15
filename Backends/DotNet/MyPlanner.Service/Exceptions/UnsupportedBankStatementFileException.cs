namespace MyPlanner.Service.Exceptions;

/// <summary>
/// The uploaded file cannot be a bank statement — most often a PDF or another document handed to
/// the CSV reader. Refused here rather than routed to the receipt (image) pipeline, which would
/// answer a bank statement with an OCR guess.
/// </summary>
public sealed class UnsupportedBankStatementFileException : Exception
{
    public UnsupportedBankStatementFileException(string message) : base(message) { }
}
