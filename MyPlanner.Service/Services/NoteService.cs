using MyPlanner.Data.Entities.Notes;
using MyPlanner.Data.UnitOfWork;

namespace MyPlanner.Service;

public class NoteService : INoteService
{
    private readonly IUnitOfWork _unitOfWork;
    public NoteService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Note?> GetAsync(Guid id)
    {
        return await Task.Run(() => _unitOfWork.Notes.GetById(id));
    }

    public async Task<bool> UpdateAsync(UpdateNoteModel model)
    {
        return await Task.Run(() =>
        {
            if (model.Id == Guid.Empty)
                return false;

            if (_unitOfWork.Notes.GetById(model.Id) is not Note note)
                return false;

            if (model.Content is not null)
            {
                note.Content = model.Content;
            }

            bool result = _unitOfWork.Notes.Update(note);
            _unitOfWork.Save();
            return result;
        });
    }
}
