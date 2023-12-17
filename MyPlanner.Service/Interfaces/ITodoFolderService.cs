using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface ITodoFolderService
{
    Task<Guid> CreateAsync(CreateFolderModel model);
    Task<IReadOnlyList<TodoFolder>> GetAllAsync();
    Task<IReadOnlyList<TodoFolder>> GetAllIncludeListsAsync();
    Task<TodoFolder?> GetAsync(Guid id);
    Task<bool> UpdateAsync(UpdateFolderModel model);
    Task<bool> DeleteAsync(Guid id);
}
