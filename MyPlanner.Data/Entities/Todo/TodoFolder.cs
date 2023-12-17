namespace MyPlanner.Data.Entities.Todo;

public class TodoFolder : EntityBase
{
    public string Title { get; set; } = string.Empty;
    public List<TodoList> Lists { get; set; } = new List<TodoList>();
}
