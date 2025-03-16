using System.Text.Json.Serialization;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.API.Models.Page;

public class CreatePageRequest
{
    public CreatePageRequest(string title, PageContentEnum pageType = PageContentEnum.Folder, Guid? parentPageId = null)
    {
        Title = title;
        PageType = pageType;
        ParentPageId = parentPageId;
    }
    public string Title { get; }

    [JsonConverter(typeof(JsonStringEnumConverter<PageContentEnum>))]
    public PageContentEnum PageType { get; }
    public Guid? ParentPageId { get; }
}
