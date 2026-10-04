namespace MyPlanner.Service.Models;

/// <summary>
/// The one upload cap, stated once and applied in every entry point: the controller before it reads a
/// stream, the finance service before anything expensive (a model call or a parse) happens, and the
/// Bucket as it hashes. Bill Images and Statement Files share it — neither is worth a separate number,
/// and one limit is one rule to remember (D11).
/// </summary>
public static class UploadLimit
{
    public const long MaxBytes = 10L * 1024 * 1024;

    /// <summary>How the limit is phrased to a user, so every refusal says the same thing.</summary>
    public const string MaxMegabytes = "10 MB";

    /// <summary>The refusal, worded the same wherever an upload is turned away.</summary>
    public static string RefusalFor(string what) =>
        $"{what} is larger than the {MaxMegabytes} upload limit.";
}
