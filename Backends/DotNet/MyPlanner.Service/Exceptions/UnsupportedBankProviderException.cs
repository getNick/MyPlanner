namespace MyPlanner.Service.Exceptions;

/// <summary>
/// No column map exists for the payment method's bank, so its statement cannot be read at all.
/// Falling back to some other bank's columns would invent dates and amounts.
/// </summary>
public sealed class UnsupportedBankProviderException : Exception
{
    public UnsupportedBankProviderException(string message) : base(message) { }
}
