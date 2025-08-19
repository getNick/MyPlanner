namespace MyPlanner.API.Models.Todo.Session;

public class CreateSessionRequest
{
    public long StartTimestamp { get; init; }
    public long EndTimestamp { get; init; }

}
