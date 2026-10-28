namespace MyPlanner.Service.Exceptions;

/// <summary>No Course with that slug is published — it is not in the Course Manifest at all. A different
/// shape from <see cref="LearningContentUnavailableException"/>: nothing is broken on disk, the slug is
/// simply not one that was ever published.</summary>
public sealed class CourseNotFoundException : Exception
{
    public CourseNotFoundException(string message) : base(message) { }
}
