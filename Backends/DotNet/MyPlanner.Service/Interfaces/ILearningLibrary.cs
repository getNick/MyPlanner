using MyPlanner.Service.Models;

namespace MyPlanner.Service.Interfaces;

/// <summary>
/// Owns the <strong>Content Mount</strong>: reads the <strong>Course Manifest</strong> from disk on
/// every call and opens only the Lesson Pages it lists. Publishing is explicit at both levels — a
/// folder or a file that the manifest does not name is not published, and nothing here decides what
/// a lesson means (Q16=B/Q17=C/Q18=C).
/// </summary>
public interface ILearningLibrary
{
    /// <summary>The published Courses in manifest order. A Course folder the manifest does not list is
    /// absent; a mount with nothing published yields an empty list rather than an error (Q20).</summary>
    Task<IReadOnlyList<LearningCourse>> GetCoursesAsync(CancellationToken cancellationToken = default);

    /// <summary>The Lesson Pages of one Course in manifest order. An unlisted <c>.html</c> sitting in the
    /// lessons folder is absent; a Course with no lessons listed yields an empty list (Q20).</summary>
    Task<IReadOnlyList<LearningLesson>> GetLessonsAsync(string courseSlug, CancellationToken cancellationToken = default);

    /// <summary>The HTML of one Lesson Page, verbatim — styles and any quiz script travel inside the file
    /// (Self-contained, Q10). Only manifest-listed <c>.html</c> files are ever opened.</summary>
    Task<string> ReadLessonPageAsync(string courseSlug, string lessonSlug, CancellationToken cancellationToken = default);
}
