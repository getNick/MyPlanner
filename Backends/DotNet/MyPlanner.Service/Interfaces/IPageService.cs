using MyPlanner.Data.Entities.Common;
using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface IPageService
{
    Task<Guid> CreateAsync(CreatePageModel model);
    Task<IReadOnlyList<Page>> GetAllAsync(string userId);
    Task<Page?> GetAsync(Guid id);
    Task<PageContentEnum> GetPageContentTypeAsync(Guid id);
    Task<PageContent?> GetPageContentAsync(Guid id);
    Task<bool> UpdateAsync(UpdatePageModel model);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> Share(SharePageModel model);
}
