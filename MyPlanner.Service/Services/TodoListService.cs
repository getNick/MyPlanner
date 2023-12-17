using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service;

public class TodoListService : ITodoListService
{
    private readonly IUnitOfWork _unitOfWork;
    public TodoListService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<Guid> CreateAsync(CreateListModel model)
    {
        return await Task.Run((() =>
        {
            if (string.IsNullOrEmpty(model.Title))
                return Guid.Empty;

            var newList = new TodoList()
            {
                Id = Guid.NewGuid(),
                Title = model.Title,
                FolderId = model.FolderId
            };
            _unitOfWork.TaskLists.Create(newList);
            _unitOfWork.Save();
            return newList.Id;
        }));
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            bool result = _unitOfWork.TaskLists.Delete(id);
            _unitOfWork.Save();
            return result;
        });
    }

    public async Task<IReadOnlyList<TodoList>> GetAllAsync(Guid folderId)
    {
        return await Task.Run(() => _unitOfWork.TaskLists.Get().ToArray());
    }

    public async Task<TodoList?> GetAsync(Guid id)
    {
        return await Task.Run(() => _unitOfWork.TaskLists.GetById(id));
    }

    public async Task<TodoList?> GetIncludeTasksAsync(Guid id)
    {
        return await Task.Run(
            () => _unitOfWork.TaskLists
            .Get(x=>x.Id == id)
            .Include(x=>x.Tasks)
            .FirstOrDefaultAsync());
    }

    public async Task<IReadOnlyList<TodoList>> GetListOutsideOfFolderAsync()
    {
         return await Task.Run(
            () => _unitOfWork.TaskLists
            .Get(x=>x.FolderId == null)
            .ToArray());
    }

    public async Task<bool> UpdateAsync(UpdateListModel model)
    {
        return await Task.Run(() =>
        {
            if(model.Id == Guid.Empty)
                return false;

            if(_unitOfWork.TaskLists.GetById(model.Id) is not TodoList list)
                return false;

            if(model.Title is not null){
                list.Title = model.Title;
            }
            if(model.FolderId != null && list.FolderId != model.FolderId){
                list.FolderId = model.FolderId;
            }
            
            bool result = _unitOfWork.TaskLists.Update(list);
            _unitOfWork.Save();
            return result;
        });
    }
}
