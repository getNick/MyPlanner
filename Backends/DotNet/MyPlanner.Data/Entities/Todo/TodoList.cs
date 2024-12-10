using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Data.Entities.Todo;

public class TodoList : PageContent
{
    public TodoList(Guid pageId) : base(pageId)
    {
        Type = PageContentEnum.TodoList;
    }
    public List<TodoTask> Tasks { get; set; } = new List<TodoTask>();
}
