using MyPlanner.Data.Entities.Notes;
using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service;

public interface INoteService
{
    Task<Note?> GetAsync(Guid id);
    Task<bool> UpdateAsync(UpdateNoteModel model);
}
