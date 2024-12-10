using System.Text.Json.Serialization;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Service;

public class SharePageModel
{
    public SharePageModel(Guid pageId, string userId)
    {
        PageId = pageId;
        UserId = userId;
    }
    public string UserId { get; }
    public Guid PageId { get; }
}
