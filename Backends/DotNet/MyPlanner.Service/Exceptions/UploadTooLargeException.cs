using MyPlanner.Service.Models;

namespace MyPlanner.Service.Exceptions;

/// <summary>An upload is bigger than the shared cap, so it is refused before anything reads it.</summary>
public sealed class UploadTooLargeException : Exception
{
    public UploadTooLargeException(string what)
        : base(UploadLimit.RefusalFor(what))
    {
    }
}
