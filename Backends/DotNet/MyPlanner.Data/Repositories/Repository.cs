using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities;

namespace MyPlanner.Data.Repositories;

public class Repository<T> : IRepository<T> where T : EntityBase
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<T> _dbSet;

    public Repository(DbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = _dbContext.Set<T>();
    }

    public int Count => _dbSet.Count();
    public IQueryable<T> Get() => _dbSet.AsQueryable();

    public T? GetById(Guid id) => _dbSet.Find(id);

    public IQueryable<T> Get(Expression<Func<T, bool>> filter = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null, string includeProperties = "")
    {
        IQueryable<T> query = _dbSet;

        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (!string.IsNullOrWhiteSpace(includeProperties))
        {
            foreach (var includeProperty in includeProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }
        }

        if (orderBy != null)
            return orderBy(query).AsQueryable();
        return query.AsQueryable();
    }

    public bool Contains(Expression<Func<T, bool>> predicate) => _dbSet.Any(predicate);


    public T Find(Expression<Func<T, bool>> predicate) => _dbSet.FirstOrDefault(predicate);

    public T Create(T entity)
    {
        var newEntry = _dbSet.Add(entity);
        return newEntry.Entity;
    }

    public bool Delete(Guid id)
    {
        var entityToDelete = _dbSet.Find(id);
        _dbSet.Remove(entityToDelete);
        return true;
    }

    public bool Delete(Expression<Func<T, bool>> predicate)
    {
        var toRemove = _dbSet.Where(predicate);
        foreach (var item in toRemove)
        {
            _dbSet.Remove(item);
        }
        return true;
    }

    public bool Update(T entity)
    {
        var entry = _dbContext.Entry(entity);
        _dbSet.Attach(entity);
        entry.State = EntityState.Modified;
        return true;
    }
}
