using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface ITodoListService
{
    Task<Guid> CreateAsync(CreateListModel model);
    Task<IReadOnlyList<TodoList>> GetAllAsync(Guid folderId);
    Task<IReadOnlyList<TodoList>> GetListOutsideOfFolderAsync();
    Task<TodoList?> GetAsync(Guid id);
    Task<TodoList?> GetIncludeTasksAsync(Guid id);
    Task<bool> UpdateAsync(UpdateListModel model);
    Task<bool> DeleteAsync(Guid id);
}
