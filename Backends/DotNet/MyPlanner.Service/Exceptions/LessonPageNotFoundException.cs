namespace MyPlanner.Service.Exceptions;

/// <summary>No Lesson Page with that slug is published in that Course. As with an unknown Course slug,
/// nothing on disk is wrong — the page was never listed.</summary>
public sealed class LessonPageNotFoundException : Exception
{
    public LessonPageNotFoundException(string message) : base(message) { }
}
