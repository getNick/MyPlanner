namespace MyPlanner.Data.Entities.Todo;

public class TodoList : EntityBase
{
    public string Title { get; set; } = string.Empty;
    public List<TodoTask> Tasks { get; set; } = new List<TodoTask>();
    public Guid? FolderId { get; set; }
}
