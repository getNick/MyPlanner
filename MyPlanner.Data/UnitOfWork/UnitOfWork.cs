using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Common;
using MyPlanner.Data.Entities.Notes;
using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;

namespace MyPlanner.Data.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    public UnitOfWork(DbContext dbContext)
    {
        _dbContext = dbContext;
        Pages = new Repository<Page>(_dbContext);
        PageContent = new Repository<PageContent>(_dbContext);
        PageSharing = new Repository<PageSharing>(_dbContext);
        TaskLists = new Repository<TodoList>(_dbContext);
        Tasks = new Repository<TodoTask>(_dbContext);
        Notes = new Repository<Note>(_dbContext);
    }

    public IRepository<Page> Pages { get; }
    public IRepository<PageContent> PageContent { get; }
    public IRepository<PageSharing> PageSharing { get; }
    public IRepository<TodoList> TaskLists { get; }
    public IRepository<TodoTask> Tasks { get; }
    public IRepository<Note> Notes { get; }
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
