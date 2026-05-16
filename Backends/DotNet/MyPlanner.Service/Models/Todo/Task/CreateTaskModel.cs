namespace MyPlanner.Service;

public class CreateTaskRequest
{
    public CreateTaskRequest(string title, Guid listId)
    {
        Title = title;
        ListId = listId;
    }
    public string Title{get;}
    public Guid ListId{get;}
}
