namespace MyPlanner.Service;

public class UpdateTaskModel
{
    public Guid Id{get;}
    public string? Title{get;}
    public string? Description{get;}
    public bool? IsComplete{get;set;}
    public Guid? ListId{get;}
}
