namespace MyPlanner.Service.Exceptions;

/// <summary>
/// Something the Course Manifest depends on is not there to read — the manifest itself, a Course
/// folder it names, or a Lesson Page file it lists. This is deliberately not an empty list: "nothing
/// published yet" and "the library is misconfigured" must not look the same on screen (Q20), so the
/// message always names the path that could not be read.
/// </summary>
public sealed class LearningContentUnavailableException : Exception
{
    public LearningContentUnavailableException(string message) : base(message) { }
}
