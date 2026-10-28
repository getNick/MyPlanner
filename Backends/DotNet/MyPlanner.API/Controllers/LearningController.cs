using Microsoft.AspNetCore.Mvc;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Interfaces;

namespace MyPlanner.API;

/// <summary>
/// Serves the <strong>Content Mount</strong>: published Courses, their Lesson Pages, and the pages'
/// HTML. This is the one anonymous endpoint in the API (Q9) — an <c>&lt;iframe&gt;</c> performs a plain
/// browser navigation and sends no <c>Authorization</c> header, so a protected endpoint would answer 401
/// and the frame would show nothing. The consequence is accepted: anything placed in a published Course
/// folder is readable by anyone who can reach the API, so nothing private belongs there.
/// </summary>
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)] // Q14: freshness never depends on revalidation
public class LearningController : ControllerBase
{
    private readonly ILearningLibrary _library;

    public LearningController(ILearningLibrary library)
    {
        _library = library;
    }

    /// <summary>The published Courses in manifest order. Empty means nothing is published yet, which is a
    /// calm message on screen rather than an error (Q20).</summary>
    [HttpGet(ApiEndpoints.Learning.Courses)]
    public async Task<IActionResult> GetCourses(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _library.GetCoursesAsync(cancellationToken));
        }
        catch (LearningContentUnavailableException error)
        {
            return ContentUnavailable(error.Message);
        }
    }

    /// <summary>The Lesson Pages of one Course in manifest order. Empty means the Course has no lessons
    /// listed yet.</summary>
    [HttpGet(ApiEndpoints.Learning.Lessons)]
    public async Task<IActionResult> GetLessons(string courseSlug, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _library.GetLessonsAsync(courseSlug, cancellationToken));
        }
        catch (CourseNotFoundException error)
        {
            return NotFound(new { error = error.Message });
        }
        catch (LearningContentUnavailableException error)
        {
            return ContentUnavailable(error.Message);
        }
    }

    /// <summary>One Lesson Page's HTML, verbatim — Self-contained, so styles and any quiz script are inside
    /// the file and outbound links are allowed to be dead (Q10).</summary>
    [HttpGet(ApiEndpoints.Learning.LessonPage)]
    public async Task<IActionResult> GetLessonPage(string courseSlug, string lessonSlug, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _library.ReadLessonPageAsync(courseSlug, lessonSlug, cancellationToken);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception error) when (error is CourseNotFoundException or LessonPageNotFoundException)
        {
            // Either half of the pair may be missing: an unpublished Course, or a slug nobody listed.
            // Both read the same way on screen and name what was asked for.
            return NotFound(new { error = error.Message });
        }
        catch (LearningContentUnavailableException error)
        {
            // A manifest that points at a folder or file which is not there is a broken mount, and the
            // message names the path so the fix is obvious from the page (Q20).
            return ContentUnavailable(error.Message);
        }
    }

    /// <summary>The library cannot read what the Course Manifest says is there. The body names the path,
    /// because "no lessons" and "the mount is broken" must not look alike on screen (Q20).</summary>
    private IActionResult ContentUnavailable(string message)
        => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = message });
}
