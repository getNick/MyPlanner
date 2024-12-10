namespace MyPlanner.Data.Entities.Common;

public class PageSharing : EntityBase
{
    public Guid PageId { get; set; } = Guid.Empty;
    public string SharedWithUserId { get; set; } = string.Empty;
}
