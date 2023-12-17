namespace MyPlanner.Service;

public class CreateListModel
{
    public CreateListModel(string title, Guid? folderId)
    {
        Title = title;
        FolderId = folderId;
    }

    public string Title{get;}
    public Guid? FolderId{get;}
}
