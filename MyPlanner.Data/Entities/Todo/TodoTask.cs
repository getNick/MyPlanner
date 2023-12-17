namespace MyPlanner.Data.Entities.Todo;

public class TodoTask : EntityBase
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public Guid ListId { get; set; }
}

