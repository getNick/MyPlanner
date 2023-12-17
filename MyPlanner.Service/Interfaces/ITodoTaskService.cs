using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface ITodoTaskService
{
    Task<Guid> CreateAsync(CreateTaskModel model);
    Task<IReadOnlyList<TodoTask>> GetAllAsync(Guid listId);
    Task<TodoTask?> GetAsync(Guid id);
    Task<bool> UpdateAsync(UpdateTaskModel model);
    Task<bool> DeleteAsync(Guid id);
}
