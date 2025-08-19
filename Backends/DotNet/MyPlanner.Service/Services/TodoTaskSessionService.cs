using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service;

public class TodoTaskSessionService : ITodoTaskSessionService
{
    private readonly IUnitOfWork _unitOfWork;
    public TodoTaskSessionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<bool> CreateAsync(TodoTaskSession session)
    {
        return await Task.Run(() =>
         {
             var task = _unitOfWork.Tasks.GetById(session.TodoTaskId);
             if (task == null)
                 return false;

             _unitOfWork.TaskSessions.Create(session);
             _unitOfWork.Save();
             return true;
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

    public async Task<bool> UpdateAsync(TodoTaskSession session)
    {
        if (session.Start.HasValue == false)
            return false;

        if (session.End.HasValue && session.End.Value < session.Start.Value)
            return false;

        return await Task.Run(() =>
         {
             var isSessionExists = _unitOfWork.TaskSessions.GetById(session.Id) is not null;
             if (!isSessionExists)
                 return false;

             _unitOfWork.TaskSessions.Update(session);
             _unitOfWork.Save();
             return true;
         });
    }

    public async Task<bool> DeleteSessionAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            bool result = _unitOfWork.TaskSessions.Delete(id);
            _unitOfWork.Save();
            return result;
        });
    }
}
