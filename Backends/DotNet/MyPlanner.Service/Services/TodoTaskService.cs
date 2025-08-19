using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service;

public class TodoTaskService : ITodoTaskService
{
    private readonly IUnitOfWork _unitOfWork;
    public TodoTaskService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<Guid> CreateAsync(CreateTaskModel model)
    {
        return await Task.Run((() =>
        {
            if (string.IsNullOrEmpty(model.Title))
                return Guid.Empty;

            var newTask = new TodoTask()
            {
                Id = Guid.NewGuid(),
                Title = model.Title,
                ListId = model.ListId,
            };
            _unitOfWork.Tasks.Create(newTask);
            _unitOfWork.Save();
            return newTask.Id;
        }));
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            bool result = _unitOfWork.Tasks.Delete(id);
            _unitOfWork.Save();
            return result;
        });
    }

    public async Task<IReadOnlyList<TodoTask>> GetAllAsync(Guid listId)
    {
        return await Task.Run(() => _unitOfWork.Tasks.Get(x => x.ListId == listId).ToArray());
    }

    public async Task<TodoTask?> GetAsync(Guid id)
    {
        return await Task.Run(() => _unitOfWork.Tasks.GetById(id));
    }

    public async Task<bool> UpdateAsync(UpdateTaskModel model)
    {
        return await Task.Run(() =>
        {
            if (model.Id == Guid.Empty)
                return false;

            if (_unitOfWork.Tasks.GetById(model.Id) is not TodoTask task)
                return false;

            if (model.Title is not null)
            {
                task.Title = model.Title;
            }
            if (model.Description is not null)
            {
                task.Description = model.Description;
            }
            if (model.IsComplete is not null)
            {
                task.IsComplete = model.IsComplete.Value;
            }
            if (model.ListId is not null)
            {
                task.ListId = model.ListId.Value;
            }

            bool result = _unitOfWork.Tasks.Update(task);
            _unitOfWork.Save();
            return result;
        });
    }
}
