using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Service;

public class UpdatePageModel
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public PageContentEnum? PageType { get; }
}
