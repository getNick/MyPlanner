namespace MyPlanner.Service;

public class CreateTaskModel
{
    public CreateTaskModel(string title, Guid listId)
    {
        Title = title;
        ListId = listId;
    }
    public string Title{get;}
    public Guid ListId{get;}
}
