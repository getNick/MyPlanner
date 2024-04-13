namespace MyPlanner.Data.Entities.Common;

public class Page : EntityBase
{
    public string Title { get; set; } = string.Empty;
    //Owner
    public string UserId { get; set; } = string.Empty;

    // Page to Page reference
    public List<Page> IncludePages { get; set; } = new List<Page>();
    public Guid? ParentPage { get; set; }

    // Content 
    public PageContent? Content { get; set; }
}
