using MyPlanner.Data.Entities.Common;
using MyPlanner.Data.Entities.Notes;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;

namespace MyPlanner.Data.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IRepository<Page> Pages { get; }
    IRepository<PageContent> PageContent { get; }
    IRepository<PageSharing> PageSharing { get; }
    IRepository<TodoList> TaskLists { get; }
    IRepository<TodoTask> Tasks { get; }
    IRepository<Note> Notes { get; }
    void Save();
}
