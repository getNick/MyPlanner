using System.Text.Json;
using Microsoft.Extensions.Options;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;

namespace MyPlanner.Service;

/// <summary>
/// Reads the Content Mount: one <strong>Course Manifest</strong> (<c>courses.json</c>) in its root,
/// Lesson Pages under each Course. The manifest is read from disk on every call, which is what makes
/// "add a page, refresh the browser" true without a rebuild or a restart.
/// </summary>
public sealed class LearningLibrary : ILearningLibrary
{
    private readonly string _root;

    /// <summary>The one file that decides what is published. Inside the mount, so editing it needs no restart.</summary>
    private string ManifestPath => Path.Combine(_root, "courses.json");

    public LearningLibrary(IOptions<LearningOptions> options)
    {
        _root = Path.GetFullPath((options.Value ?? new LearningOptions()).ContentPath ?? LearningOptions.DefaultPath);
    }

    public async Task<IReadOnlyList<LearningCourse>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await ReadManifestAsync(cancellationToken);

        // A listed Course whose folder is not on disk is an error naming that folder, not a shorter
        // list: the manifest and the synced folder are two sources describing one Course, and the pair
        // breaking is exactly what the user needs to see (Q20).
        var courses = new List<LearningCourse>();
        foreach (var course in manifest.Courses)
        {
            RequireCourseFolder(course);
            courses.Add(new LearningCourse(course.Slug, course.Title));
        }
        return courses;
    }

    public async Task<IReadOnlyList<LearningLesson>> GetLessonsAsync(string courseSlug, CancellationToken cancellationToken = default)
    {
        var manifest = await ReadManifestAsync(cancellationToken);
        return PublishedPages(FindCourse(manifest, courseSlug))
            .Select(p => new LearningLesson(Path.GetFileNameWithoutExtension(p.File), p.Title))
            .ToList();
    }

    public async Task<string> ReadLessonPageAsync(string courseSlug, string lessonSlug, CancellationToken cancellationToken = default)
    {
        var manifest = await ReadManifestAsync(cancellationToken);
        var course = FindCourse(manifest, courseSlug);

        // The file opened is always the one the manifest names — a slug never becomes a path.
        var page = FindPage(PublishedPages(course), lessonSlug);
        var path = ResolveWithinMount(Path.Combine(RequireCourseFolder(course), LessonsSubfolder(course)), page.File);

        if (!File.Exists(path))
            throw new LearningContentUnavailableException(
                $"The Course Manifest lists '{page.File}' for Course '{course.Slug}', but {path} is not on disk — the file has not synced yet.");

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>
    /// Resolves a manifest-relative path and refuses it if the result lands outside the Content Mount.
    /// The mount is read-only and served anonymously, so a manifest (or a synced folder) that names its
    /// way upwards must never reach the disk (Risk: anonymous read surface).
    /// </summary>
    private string ResolveWithinMount(string basePath, string leaf)
    {
        var combined = Path.GetFullPath(Path.Combine(basePath, leaf));
        var boundary = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

        if (combined != _root && !combined.StartsWith(boundary, StringComparison.Ordinal))
            throw new LearningContentUnavailableException(
                $"'{leaf}' resolves to {combined}, outside the Content Mount at {_root}; refusing to read it.");

        return combined;
    }

    /// <summary>The pages a Course lists, refused if any of them is not an HTML Lesson Page. An .md in
    /// the vault is Obsidian source, and a manifest that names one is wrong out loud rather than a page
    /// that quietly never appears.</summary>
    private static List<ManifestPage> PublishedPages(ManifestCourse course)
    {
        var pages = course.Pages ?? [];
        foreach (var page in pages)
            if (!string.Equals(Path.GetExtension(page.File), ".html", StringComparison.OrdinalIgnoreCase))
                throw new LearningContentUnavailableException(
                    $"The Course Manifest lists '{page.File}' for Course '{course.Slug}', but only .html Lesson Pages are served.");

        return pages;
    }

    /// <summary>The published Lesson Page for a slug, matched against the manifest listing only.</summary>
    private static ManifestPage FindPage(List<ManifestPage> pages, string lessonSlug)
    {
        var page = pages.FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p.File), lessonSlug, StringComparison.OrdinalIgnoreCase));
        if (page is null)
            throw new LessonPageNotFoundException($"No published Lesson Page with slug '{lessonSlug}'.");
        return page;
    }

    /// <summary>Where a Course lives inside the Content Mount, refusing anything that resolves out of
    /// the mount. Defaults to the mount root itself, which is how a single-course mount (one synced
    /// vault folder) reads.</summary>
    private string RequireCourseFolder(ManifestCourse course)
    {
        var folder = ResolveWithinMount(_root, course.Folder ?? ".");
        if (!Directory.Exists(folder))
            throw new LearningContentUnavailableException(
                $"The Course Manifest lists Course '{course.Slug}' in folder '{course.Folder ?? "."}', but {folder} is not in the Content Mount.");
        return folder;
    }

    /// <summary>The lessons subfolder inside a Course folder; "lessons" unless the manifest says otherwise.</summary>
    private static string LessonsSubfolder(ManifestCourse course) => course.Lessons ?? "lessons";

    /// <summary>The published Course for a slug. A slug is matched only against the manifest, never
    /// turned into a path — which is what keeps the anonymous read surface to the published set (Q9).</summary>
    private static ManifestCourse FindCourse(CourseManifestFile manifest, string courseSlug)
    {
        var course = manifest.Courses.FirstOrDefault(c => string.Equals(c.Slug, courseSlug, StringComparison.OrdinalIgnoreCase));
        if (course is null)
            throw new CourseNotFoundException($"No published Course with slug '{courseSlug}'.");
        return course;
    }

    private async Task<CourseManifestFile> ReadManifestAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ManifestPath))
            throw new LearningContentUnavailableException(
                $"No Course Manifest to read: {ManifestPath} does not exist in the Content Mount.");

        try
        {
            await using var stream = File.OpenRead(ManifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<CourseManifestFile>(stream, ManifestJson.Options, cancellationToken);
            return manifest ?? new CourseManifestFile([]);
        }
        catch (JsonException error)
        {
            // A half-synced or mistyped manifest is the most likely way this breaks, and it reads as
            // "the library vanished" unless the message says which file is unreadable.
            throw new LearningContentUnavailableException($"The Course Manifest at {ManifestPath} cannot be read: {error.Message}");
        }
    }

    /// <summary>The shape of <c>courses.json</c>. Field names are the manifest's public contract with the
    /// vault folder, so they stay exactly as written there.</summary>
    private sealed record CourseManifestFile(List<ManifestCourse> Courses);

    private sealed record ManifestCourse(string Slug, string Title, string? Folder, string? Lessons, List<ManifestPage>? Pages);

    private sealed record ManifestPage(string File, string Title);

    private static class ManifestJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
    }
}
