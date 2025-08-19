using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface ITodoTaskSessionService
{
    Task<bool> CreateAsync(TodoTaskSession session);
    Task<bool> StartSessionAsync(Guid taskId, DateTime timeStamp);
    Task<bool> StopSessionAsync(Guid taskId, DateTime timeStamp);
    Task<IReadOnlyList<TodoTaskSession>> GetAllSessionsAsync(Guid taskId);
    Task<bool> UpdateAsync(TodoTaskSession model);
    Task<bool> DeleteSessionAsync(Guid id);
}
