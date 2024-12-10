using MyPlanner.Data.Entities.Common;

namespace MyPlanner.Data.Entities.Notes;

public class Note : PageContent
{
    public Note(Guid pageId) : base(pageId)
    {
        Type = PageContentEnum.Note;
    }
    public string Content { get; set; } = string.Empty;
}

