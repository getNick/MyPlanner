namespace MyPlanner.Service.Models;

/// <summary>
/// Where the <strong>Bucket</strong> lives — the single flat folder every stored upload lands in
/// (Bill Images and Statement Files together, distinguished only by extension).
/// </summary>
public class BucketOptions
{
    /// <summary>Fallback used when <c>STORAGE_PATH</c> is unset. From the API project directory this
    /// resolves to the solution root, alongside the default database volume folder.</summary>
    public const string DefaultPath = "../../../../storage";

    public string Path { get; set; } = DefaultPath;
}
