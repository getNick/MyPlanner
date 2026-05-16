namespace MyPlanner.Service.Models.Todo.Task;

public class TaskSessionResponse
{
    public Guid Id { get; set; } = Guid.Empty;
    public long? StartTimestamp { get; init; }
    public long? EndTimestamp { get; init; }
}
