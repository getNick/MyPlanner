namespace MyPlanner.API.Models.Todo.Session;

public class UpdateSessionRequest
{
    public long? StartTimestamp { get; init; }
    public long? EndTimestamp { get; init; }
}
