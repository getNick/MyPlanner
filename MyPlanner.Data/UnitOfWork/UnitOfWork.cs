using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;

namespace MyPlanner.Data.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    public UnitOfWork(DbContext dbContext)
    {
        _dbContext = dbContext;
        Folders = new Repository<TodoFolder>(_dbContext);
        TaskLists = new Repository<TodoList>(_dbContext);
        Tasks = new Repository<TodoTask>(_dbContext);
    }

    public IRepository<TodoFolder> Folders { get; }
    public IRepository<TodoList> TaskLists { get; }
    public IRepository<TodoTask> Tasks { get; }
    public void Save()
    {
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _dbContext.Dispose();
            }
        }
        _disposed = true;
    }

    private bool _disposed;
    private DbContext _dbContext;
}
