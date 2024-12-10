namespace MyPlanner.Service;

public class UpdateTaskModel
{
    public Guid Id{get;set;}
    public string? Title{get;set;}
    public string? Description{get;set;}
    public bool? IsComplete{get;set;}
    public Guid? ListId{get;set;}
}
