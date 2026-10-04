namespace MyPlanner.Service.Models;

/// <summary>
/// Where the <strong>Bucket</strong> lives — the single flat folder every stored upload lands in
/// (Bill Images and Statement Files together, distinguished only by extension).
/// </summary>
public class BucketOptions
{
    /// <summary>Fallback used when <c>STORAGE_PATH</c> is unset. Inside a container that fallback is
    /// ephemeral, so the bind mount in docker-compose.yaml is not optional even though the variable is.</summary>
    public const string DefaultPath = "./storage";

    public string Path { get; set; } = DefaultPath;
}
