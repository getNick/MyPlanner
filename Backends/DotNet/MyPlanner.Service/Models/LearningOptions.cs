namespace MyPlanner.Service.Models;

/// <summary>
/// Where the <strong>Content Mount</strong> lives — the synced folder whose published children are
/// <strong>Courses</strong>, served to <c>/learning</c> without rebuilding or restarting anything.
/// It is a different folder from The Bucket on purpose: The Bucket is content the app writes, this is
/// content the app only reads (Q4).
/// </summary>
public class LearningOptions
{
    /// <summary>Fallback used when <c>LEARNING_CONTENT_PATH</c> is unset. From the API project
    /// directory this resolves to the solution root, alongside the default Bucket folder.</summary>
    public const string DefaultPath = "../../../../learning";

    public string ContentPath { get; set; } = DefaultPath;
}
