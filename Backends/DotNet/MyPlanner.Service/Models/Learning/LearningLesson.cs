namespace MyPlanner.Service.Models;

/// <summary>One published <strong>Lesson Page</strong>: a Self-contained HTML file listed in the
/// Course Manifest. <c>Slug</c> is the file name without its extension, which is what appears in
/// <c>/learning/{courseSlug}/{lessonSlug}</c>; order is the manifest array order (Q21=A).</summary>
public sealed record LearningLesson(string Slug, string Title);
