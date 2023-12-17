using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;

namespace MyPlanner.Data.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IRepository<TodoFolder> Folders { get; }
    IRepository<TodoList> TaskLists { get; }
    IRepository<TodoTask> Tasks { get; }
    void Save();
}
