using System.Text.Json.Serialization;

namespace MyPlanner.Data.Entities.Common;

public abstract class PageContent : EntityBase
{
    protected PageContent(Guid pageId)
    {
        PageId = pageId;
    }
    [JsonConverter(typeof(JsonStringEnumConverter<PageContentEnum>))]
    public PageContentEnum Type { get; set; }
    public Guid PageId { get; set; }
}
