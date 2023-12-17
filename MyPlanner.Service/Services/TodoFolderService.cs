using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service;

public class TodoFolderService : ITodoFolderService
{
    private readonly IUnitOfWork _unitOfWork;
    public TodoFolderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<Guid> CreateAsync(CreateFolderModel model)
    {
        return await Task.Run((() =>
        {
            if (string.IsNullOrEmpty(model.Title))
                return Guid.Empty;

            var newFolder = new TodoFolder()
            {
                Id = Guid.NewGuid(),
                Title = model.Title,
            };
            _unitOfWork.Folders.Create(newFolder);
            _unitOfWork.Save();
            return newFolder.Id;
        }));
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            bool result = _unitOfWork.Folders.Delete(id);
            _unitOfWork.Save();
            return result;
        });
    }

    public async Task<IReadOnlyList<TodoFolder>> GetAllAsync()
    {
        return await Task.Run(() => _unitOfWork.Folders.Get().ToArray());
    }

    public async Task<IReadOnlyList<TodoFolder>> GetAllIncludeListsAsync()
    {
        return await Task.Run(() => _unitOfWork.Folders.Get().Include(x=>x.Lists).ToArray());
    }

    public async Task<TodoFolder?> GetAsync(Guid id)
    {
        return await Task.Run(() => _unitOfWork.Folders.GetById(id));
    }

    public async Task<bool> UpdateAsync(UpdateFolderModel model)
    {
        return await Task.Run(() =>
        {
            if(model.Id == Guid.Empty)
                return false;

            if(_unitOfWork.Folders.GetById(model.Id) is not TodoFolder folder)
                return false;

            if(model.Title is not null){
                folder.Title = model.Title;
            }
            
            bool result = _unitOfWork.Folders.Update(folder);
            _unitOfWork.Save();
            return result;
        });
    }
}
