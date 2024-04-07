using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.Entities.Common;
using MyPlanner.Data.Entities.Notes;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service;

public class PageService : IPageService
{
    private readonly IUnitOfWork _unitOfWork;
    public PageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<Guid> CreateAsync(CreatePageModel model)
    {
        return await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(model.Title))
                return Guid.Empty;

            var newFolder = new Page()
            {
                Id = Guid.NewGuid(),
                Title = model.Title,
                UserId = model.UserId,
                ParentPage = model.ParentPageId
            };

            PageContent? pageContent = CreatePageContent(model.PageType, newFolder.Id);
            if (pageContent != null)
            {
                _unitOfWork.PageContent.Create(pageContent);
                newFolder.Content = pageContent;
            }

            _unitOfWork.Pages.Create(newFolder);
            _unitOfWork.Save();
            return newFolder.Id;
        });
    }

    private PageContent? CreatePageContent(PageContentEnum type, Guid pageId)
    {
        return type switch
        {
            PageContentEnum.TodoList => _unitOfWork.TaskLists.Create(new TodoList(pageId)),
            PageContentEnum.Note => _unitOfWork.Notes.Create(new Note(pageId)),
            PageContentEnum.Folder => null,
            _ => throw new NotImplementedException(),
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            bool result = _unitOfWork.Pages.Delete(id);
            _unitOfWork.Save();
            return result;
        });
    }

    public Page LoadSubPages(Page page)
    {
        var subPages = _unitOfWork.Pages.Get(x => x.ParentPage == page.Id).Include(x => x.Content).ToList();
        page.IncludePages = subPages;
        page.IncludePages.ForEach(x => LoadSubPages(x));
        return page;
    }
    public async Task<IReadOnlyList<Page>> GetAllAsync(string userId)
    {
        return await Task.Run(() =>
        {
            var result = _unitOfWork.Pages.Get(x => string.Equals(x.UserId, userId) && x.ParentPage == null)
            .Include(x => x.Content)
            .ToArray()
            .Select(LoadSubPages)
            .ToArray();
            return result;
        });
    }

    public async Task<Page?> GetAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            var page = _unitOfWork.Pages.GetById(id);
            return page != null ? LoadSubPages(page) : null;
        });
    }
    public async Task<PageContentEnum> GetPageContentTypeAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            PageContent? pageContent = _unitOfWork.PageContent.Get(x => x.PageId == id).FirstOrDefault();
            return pageContent?.Type ?? PageContentEnum.Folder;
        });
    }
    public async Task<PageContent?> GetPageContentAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            PageContent pageContent = _unitOfWork.PageContent.Find(x => x.PageId == id);

            PageContent? content = pageContent.Type switch
            {
                PageContentEnum.TodoList => _unitOfWork.TaskLists.Get(x => x.Id == pageContent.Id).Include(x => x.Tasks).FirstOrDefault(),
                PageContentEnum.Note => _unitOfWork.Notes.Get(x => x.Id == pageContent.Id).FirstOrDefault(),
                PageContentEnum.Folder => null,
                _ => throw new NotImplementedException()
            };
            return content;
        });
    }

    public async Task<bool> UpdateAsync(UpdatePageModel model)
    {
        return await Task.Run(() =>
        {
            if (model.Id == Guid.Empty)
                return false;

            if (_unitOfWork.Pages.GetById(model.Id) is not Page page)
                return false;

            if (model.Title is not null)
            {
                page.Title = model.Title;
            }

            bool result = _unitOfWork.Pages.Update(page);
            _unitOfWork.Save();
            return result;
        });
    }
}
