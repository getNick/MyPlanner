using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MyPlanner.Service;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services;

/*
 * ═══════════════════════════════════════════════════════════
 *  LearningLibraryTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [ ] = not yet implemented
 *
 * The seam is the Learning module itself: GetCoursesAsync() → Courses, GetLessonsAsync(courseSlug)
 * → Lesson Pages, ReadLessonPageAsync(courseSlug, lessonSlug) → HTML text, all over a throwaway
 * Content Mount holding a Course Manifest. Nothing here knows about HTTP — status codes and the
 * no-store header live in the thin LearningController above this seam — the repo has no
 * WebApplicationFactory harness to exercise HTTP from outside, so those stay uncovered here.
 *
 * ────────────────────────────────────────────────────────────
 *  Publishing is explicit (Q16=B, Q17=C, Q18=C, Q21=A)
 * ────────────────────────────────────────────────────────────
 *   [x] the manifest decides which folders are Courses, and array order is the reading order
 *   [x] Courses come back in manifest array order — that order IS the sequence (Q21=A)
 *   [x] a Course folder on disk that the manifest does not list stays unpublished
 *   [x] Lesson Pages come back in manifest array order, with the slug taken from the file name
 *   [x] an unlisted .html inside a published lessons folder is not published (Q17=C)
 *   [x] a Course with no `pages` key at all is an empty lesson list, not an error
 *   [x] page titles come from the manifest, not from <title> in the file (Q18=C)
 *
 * ────────────────────────────────────────────────────────────
 *  Read per request, so no rebuild and no restart (the core requirement)
 * ────────────────────────────────────────────────────────────
 *   [x] adding a manifest line between two calls on the same instance appears without recreating it
 *   [x] the Lesson Page HTML comes back byte-for-byte, styles and all (Q10)
 *
 * ────────────────────────────────────────────────────────────
 *  Two failure shapes, kept apart (Q20)
 * ────────────────────────────────────────────────────────────
 *   [x] an empty manifest is an empty Course list, not an error
 *   [x] a published Course with no lessons listed is an empty lesson list, not an error
 *   [x] a missing or unreadable courses.json names the path it could not read
 *   [x] a manifest pointing at a folder that does not exist names that folder
 *   [x] a manifest listing a file that is not on disk names that file (a forgotten sync, visible)
 *   [x] an unknown Course slug and an unlisted Lesson Page are "not published", not a broken mount
 *
 * ────────────────────────────────────────────────────────────
 *  The anonymous read surface stays narrow (Q9)
 * ────────────────────────────────────────────────────────────
 *   [x] a manifest entry whose path escapes the Content Mount is refused and nothing is read
 *   [x] only .html is ever served — a listed .md is refused
 *
 * ────────────────────────────────────────────────────────────
 *  Should be implemented
 * ────────────────────────────────────────────────────────────
 *   [ ] HTTP-level mapping in LearningController — 503 with the named path, 404 for "not published",
 *       and `Cache-Control: no-store` on every response. The repo has no WebApplicationFactory harness,
 *       so nothing exercises these from outside; today they were checked by hand against a running API.
 */

[TestFixture]
public class LearningLibraryTests
{
    private string _mount = null!;
    private ILearningLibrary _library = null!;

    [SetUp]
    public void SetUp()
    {
        _mount = Path.Combine(Path.GetTempPath(), "myplanner-learning-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mount);
        _library = new LearningLibrary(Options.Create(new LearningOptions { ContentPath = _mount }));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_mount, recursive: true); } catch (IOException) { }
    }

    /// <summary>Writes a Course Manifest into the Content Mount root and creates the folders it names.</summary>
    private void WriteManifest(string coursesJson)
        => File.WriteAllText(Path.Combine(_mount, "courses.json"), coursesJson);

    private string CreateCourseFolder(string courseFolderName, params string[] lessonFiles)
    {
        var lessons = Path.Combine(_mount, courseFolderName, "lessons");
        Directory.CreateDirectory(lessons);
        foreach (var file in lessonFiles)
            File.WriteAllText(Path.Combine(lessons, file), $"<p>{file}</p>");
        return lessons;
    }

    // ── Publishing is explicit ───────────────────────────────────────

    [Test]
    public async Task Manifest_publishes_the_courses_it_lists()
    {
        CreateCourseFolder("guitar", "0001-first-chords.html");
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons" }
            ] }
            """);

        var courses = await _library.GetCoursesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(courses, Has.Count.EqualTo(1));
            Assert.That(courses[0].Slug, Is.EqualTo("guitar"));
            Assert.That(courses[0].Title, Is.EqualTo("How to play guitar"));
        });
    }

    [Test]
    public async Task Lessons_come_back_in_manifest_order_with_the_slug_from_the_file_name()
    {
        // Manifest order is deliberately not alphabetical-by-title, so a sort would show up.
        CreateCourseFolder("guitar", "0002-barre-chords.html", "0001-first-chords.html");
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons",
                  "pages": [
                    { "file": "0002-barre-chords.html", "title": "Barre chords" },
                    { "file": "0001-first-chords.html", "title": "First chords" }
                  ] }
            ] }
            """);

        var lessons = await _library.GetLessonsAsync("guitar");

        Assert.That(lessons, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            // Slug is the file name minus .html, verbatim; title comes from the manifest, never from the file.
            Assert.That(lessons[0].Slug, Is.EqualTo("0002-barre-chords"));
            Assert.That(lessons[0].Title, Is.EqualTo("Barre chords"));
            Assert.That(lessons[1].Slug, Is.EqualTo("0001-first-chords"));
        });
    }

    [Test]
    public async Task A_Lesson_Page_comes_back_byte_for_byte_as_the_file_itself()
    {
        // Self-contained (Q10): the style and the quiz script are inside the file, so nothing else
        // has to be served for the page to work.
        const string html = """
            <!doctype html>
            <html><head><style>body{font:18px system-ui}</style></head>
            <body><h1>First chords</h1><div class="quiz" data-answer="B"><button data-opt="A">Em</button><button data-opt="B">C</button></div>
            <script>document.querySelectorAll('.quiz').forEach(q=>q.addEventListener('click',()=>q.dataset.locked='1'))</script>
            </body></html>
            """;
        CreateCourseFolder("guitar");
        File.WriteAllText(Path.Combine(_mount, "guitar", "lessons", "0001-first-chords.html"), html);
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons",
                  "pages": [ { "file": "0001-first-chords.html", "title": "First chords" } ] }
            ] }
            """);

        var page = await _library.ReadLessonPageAsync("guitar", "0001-first-chords");

        Assert.That(page, Is.EqualTo(html));
    }

    [Test]
    public async Task A_mount_with_no_courses_json_names_the_path_it_could_not_read()
    {
        // Q20: this is the error shape, not the empty shape — the page shows a card naming the path.
        var error = Assert.ThrowsAsync<LearningContentUnavailableException>(async () => await _library.GetCoursesAsync());

        Assert.That(error!.Message, Does.Contain(Path.Combine(_mount, "courses.json")));
    }

    [Test]
    public async Task A_slug_that_is_not_in_the_manifest_is_not_published_rather_than_broken()
    {
        CreateCourseFolder("guitar", "0001-first-chords.html");
        WriteManifest("""
            { "courses": [ { "slug": "guitar", "title": "How to play guitar" } ] }
            """);

        var error = Assert.ThrowsAsync<CourseNotFoundException>(async () => await _library.GetLessonsAsync("ukulele"));

        Assert.That(error!.Message, Does.Contain("ukulele"));
    }

    [Test]
    public async Task A_lesson_page_the_manifest_never_listed_is_not_published()
    {
        CreateCourseFolder("guitar", "0001-first-chords.html", "0002-barre-chords.html");
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons",
                  "pages": [ { "file": "0001-first-chords.html", "title": "First chords" } ] }
            ] }
            """);

        // 0002 is on disk inside a published lessons folder, but unlisted files stay unpublished (Q17=C).
        Assert.ThrowsAsync<LessonPageNotFoundException>(async () => await _library.ReadLessonPageAsync("guitar", "0002-barre-chords"));
    }

    [Test]
    public async Task A_listed_Lesson_Page_that_is_not_on_disk_names_the_file_a_forgotten_sync_left_out()
    {
        CreateCourseFolder("guitar");
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons",
                  "pages": [ { "file": "0001-first-chords.html", "title": "First chords" } ] }
            ] }
            """);

        // The mirror of the unlisted case: the manifest promises a file the sync has not delivered.
        // That is a broken mount, not an unpublished page, and the message says which file is missing.
        var error = Assert.ThrowsAsync<LearningContentUnavailableException>(async () => await _library.ReadLessonPageAsync("guitar", "0001-first-chords"));

        Assert.That(error!.Message, Does.Contain(Path.Combine(_mount, "guitar", "lessons", "0001-first-chords.html")));
    }

    [Test]
    public async Task A_manifest_path_that_escapes_the_Content_Mount_is_refused()
    {
        // The anonymous read surface (Q9) stays the size of the published set: even a manifest that
        // names a file outside the mount must not get it served.
        var outside = Path.Combine(Path.GetDirectoryName(_mount)!, "outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.html"), "<p>not published</p>");
        try
        {
            CreateCourseFolder("guitar");
            WriteManifest("""
                { "courses": [
                    { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "../..",
                      "pages": [ { "file": "secret.html", "title": "Not published" } ] }
                ] }
                """);

            var error = Assert.ThrowsAsync<LearningContentUnavailableException>(async () => await _library.ReadLessonPageAsync("guitar", "secret"));

            Assert.That(error!.Message, Does.Contain("Content Mount"));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Test]
    public void A_manifest_pointing_at_a_folder_that_is_not_there_names_that_folder()
    {
        // The other half of the two-source risk (Q20): a folder renamed in Obsidian or Nextcloud breaks
        // the pairing, and it must read as a named path rather than an empty library.
        WriteManifest("""
            { "courses": [ { "slug": "guitar", "title": "How to play guitar", "folder": "guitar-course" } ] }
            """);

        var error = Assert.ThrowsAsync<LearningContentUnavailableException>(async () => await _library.GetCoursesAsync());

        Assert.That(error!.Message, Does.Contain(Path.Combine(_mount, "guitar-course")));
    }

    [Test]
    public async Task Nothing_published_yet_is_an_empty_list_rather_than_an_error()
    {
        // Q20's calm shape: the mount exists and is readable, there is simply nothing to read.
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons", "pages": [] }
            ] }
            """);
        CreateCourseFolder("guitar");

        var courses = await _library.GetCoursesAsync();
        var lessons = await _library.GetLessonsAsync("guitar");

        Assert.Multiple(() =>
        {
            Assert.That(courses, Has.Count.EqualTo(1));
            Assert.That(lessons, Is.Empty);
        });
    }

    [Test]
    public async Task Editing_the_manifest_between_two_calls_shows_up_without_restarting_anything()
    {
        // The whole point of the design: no rebuild and no restart. Same instance, second read sees
        // the new line — so a cache or a startup snapshot here would break the requirement silently.
        WriteManifest("""
            { "courses": [ { "slug": "guitar", "title": "How to play guitar", "folder": "guitar" } ] }
            """);
        CreateCourseFolder("guitar", "0001-first-chords.html");

        Assert.That(await _library.GetCoursesAsync(), Has.Count.EqualTo(1));

        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar" },
                { "slug": "ukulele", "title": "Ukulele", "folder": "ukulele" }
            ] }
            """);
        Directory.CreateDirectory(Path.Combine(_mount, "ukulele"));

        var courses = await _library.GetCoursesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(courses, Has.Count.EqualTo(2));
            Assert.That(courses[1].Slug, Is.EqualTo("ukulele"));
        });
    }

    [Test]
    public void Only_html_is_ever_served_a_listed_md_is_refused()
    {
        CreateCourseFolder("guitar", "0001-first-chords.md");
        WriteManifest("""
            { "courses": [
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar", "lessons": "lessons",
                  "pages": [ { "file": "0001-first-chords.md", "title": "First chords" } ] }
            ] }
            """);

        // An .md in the vault is Obsidian source, not a Lesson Page. Loud refusal beats silently hiding
        // the page, which is indistinguishable from a forgotten manifest line.
        var error = Assert.ThrowsAsync<LearningContentUnavailableException>(async () => await _library.ReadLessonPageAsync("guitar", "0001-first-chords"));

        Assert.That(error!.Message, Does.Contain(".html"));
    }

    [Test]
    public async Task The_manifest_decides_which_folders_are_Courses_and_in_what_order()
    {
        // Q16=B: a folder dropped into the mount is not a Course until the manifest lists it, and the
        // array order IS the reading order (Q21=A) — so a sort here would silently reshuffle a Course.
        CreateCourseFolder("ukulele", "0001-tuning.html");
        CreateCourseFolder("guitar", "0001-first-chords.html");
        Directory.CreateDirectory(Path.Combine(_mount, "some-day"));
        WriteManifest("""
            { "courses": [
                { "slug": "ukulele", "title": "Ukulele", "folder": "ukulele" },
                { "slug": "guitar", "title": "How to play guitar", "folder": "guitar" }
            ] }
            """);

        var courses = await _library.GetCoursesAsync();

        Assert.That(courses.Select(c => c.Slug), Is.EqualTo(new[] { "ukulele", "guitar" }));
    }
}
