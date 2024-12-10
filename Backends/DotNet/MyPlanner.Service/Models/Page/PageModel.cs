using System.Text.Json.Serialization;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Service;

public class PageModel
{
    public PageModel(Page page)
    {
        Id = page.Id;
        Title = page.Title;
        IncludePages = page.IncludePages.Select(page => new PageModel(page)).ToArray();
        Content = page.Content;
    }
    public Guid Id { get; }
    public string Title { get; }
    public IReadOnlyList<PageModel> IncludePages { get; }
    public PageContent? Content { get; }
}
