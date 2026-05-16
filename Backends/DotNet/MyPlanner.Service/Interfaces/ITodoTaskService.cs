using MyPlanner.Service.Models.Todo.Task;

namespace MyPlanner.Service;

public interface ITodoTaskService
{
    Task<Guid> CreateAsync(CreateTaskRequest model);
    Task<IReadOnlyList<TaskDetailsResponse>> GetAllAsync(Guid listId);
    Task<TaskDetailsResponse?> GetAsync(Guid id);
    Task<bool> UpdateAsync(UpdateTaskRequest model);
    Task<bool> DeleteAsync(Guid id);
}
