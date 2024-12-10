using System.Text.Json.Serialization;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Service;

public class CreatePageModel
{
    public CreatePageModel(string title, string userId, PageContentEnum pageType = PageContentEnum.Folder, Guid? parentPageId = null)
    {
        Title = title;
        PageType = pageType;
        UserId = userId;
        ParentPageId = parentPageId;
    }
    public string Title { get; }

    [JsonConverter(typeof(JsonStringEnumConverter<PageContentEnum>))]
    public PageContentEnum PageType { get; }
    public string UserId { get; }
    public Guid? ParentPageId { get; }
}
