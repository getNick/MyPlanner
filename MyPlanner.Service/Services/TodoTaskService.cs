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

    public async Task<Guid> CreateSessionAsync(CreateSessionModel model)
    {
        return await Task.Run(() =>
         {
             var task = _unitOfWork.Tasks.Get(x => x.Id == model.TaskId).Include(x => x.Sessions).FirstOrDefault();
             if (task == null)
                 return Guid.Empty;

             var newSession = new TodoTaskSession()
             {
                 Id = Guid.NewGuid(),
                 TodoTaskId = model.TaskId,
                 Start = model.Start,
                 End = model.End
             };
             _unitOfWork.TaskSessions.Create(newSession);
             _unitOfWork.Save();
             return newSession.Id;
         });
    }

    public async Task<bool> StartSessionAsync(Guid taskId, DateTime timeStamp)
    {
        return await Task.Run(() =>
        {
            var task = _unitOfWork.Tasks.Get(x => x.Id == taskId).Include(x => x.Sessions).FirstOrDefault();
            if (task == null || task.Sessions.Any(x => x.Start != null && x.End == null))
                return false;

            _unitOfWork.TaskSessions.Create(new TodoTaskSession()
            {
                TodoTaskId = taskId,
                Start = timeStamp,
            });
            _unitOfWork.Save();
            return true;
        });
    }

    public async Task<bool> StopSessionAsync(Guid taskId, DateTime timeStamp)
    {
        return await Task.Run(() =>
        {
            var activeSession = _unitOfWork.TaskSessions.Get(x => x.TodoTaskId == taskId).FirstOrDefault(x => x.Start != null && x.End == null);
            if (activeSession == null)
                return false;

            activeSession.End = timeStamp;
            _unitOfWork.TaskSessions.Update(activeSession);
            _unitOfWork.Save();
            return true;
        });
    }

    public async Task<IReadOnlyList<TodoTaskSession>> GetAllSessionsAsync(Guid taskId)
    {
        return await Task.Run(() =>
        {
            return _unitOfWork.TaskSessions.Get(x => x.TodoTaskId == taskId).ToArray();
        });
    }

    public Task<bool> UpdateSessionAsync(UpdateSessionModel model)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteSessionAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    Task<IReadOnlyList<TodoTask>> ITodoTaskService.GetAllSessionsAsync(Guid taskId)
    {
        throw new NotImplementedException();
    }
}
