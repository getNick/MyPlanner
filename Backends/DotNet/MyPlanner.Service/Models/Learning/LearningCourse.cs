namespace MyPlanner.Service.Models;

/// <summary>One published <strong>Course</strong>: a learning sequence with a display title and a URL
/// slug, both declared in the Course Manifest rather than read off the folder name (Q15, Q8 verified
/// fact #8 — the synced folder names contain spaces and capitals).</summary>
public sealed record LearningCourse(string Slug, string Title);
