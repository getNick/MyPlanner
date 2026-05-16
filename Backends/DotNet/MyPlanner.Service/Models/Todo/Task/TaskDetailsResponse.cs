namespace MyPlanner.Service.Models.Todo.Task;

public class TaskDetailsResponse
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public Guid ListId { get; set; }
    public long? StartedSessionTimestamp { get; init; }
    public List<TaskSessionResponse> Sessions { get; set; } = new List<TaskSessionResponse>();
}
