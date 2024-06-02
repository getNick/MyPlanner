using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface ITodoTaskService
{
    Task<Guid> CreateAsync(CreateTaskModel model);
    Task<IReadOnlyList<TodoTask>> GetAllAsync(Guid listId);
    Task<TodoTask?> GetAsync(Guid id);
    Task<bool> UpdateAsync(UpdateTaskModel model);
    Task<bool> DeleteAsync(Guid id);

    //sessions
    Task<Guid> CreateSessionAsync(CreateSessionModel model);
    Task<bool> StartSessionAsync(Guid taskId, DateTime timeStamp);
    Task<bool> StopSessionAsync(Guid taskId, DateTime timeStamp);
    Task<IReadOnlyList<TodoTask>> GetAllSessionsAsync(Guid taskId);
    Task<bool> UpdateSessionAsync(UpdateSessionModel model);
    Task<bool> DeleteSessionAsync(Guid id);
}
