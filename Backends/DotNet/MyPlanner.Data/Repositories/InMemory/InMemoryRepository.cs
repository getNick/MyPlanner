using System.Linq.Expressions;
using MyPlanner.Data.Entities;
using MyPlanner.Data.Repositories;

namespace MyPlanner.Data.Repositories.InMemory;

public class InMemoryRepository<T> : IRepository<T> where T : EntityBase
{
    private List<T> _list;
    public InMemoryRepository(List<T> list)
    {
        _list = list;
    }

    public int Count => _list.Count;
    public IQueryable<T> Get()
    {
        return _list.AsQueryable();
    }

    public T? GetById(Guid id)
    {
        return _list.FirstOrDefault(x => x.Id == id);
    }

    public IQueryable<T> Get(Expression<Func<T, bool>> filter = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null, string includeProperties = "")
    {
        var query = _list.AsQueryable();
        if (filter != null)
            query = query.Where(filter);

        if (orderBy != null)
            return orderBy(query).AsQueryable();
        return query.AsQueryable();
    }

    public T Find(Expression<Func<T, bool>> predicate)
    {
        return _list.AsQueryable().FirstOrDefault(predicate);
    }

    public bool Contains(Expression<Func<T, bool>> predicate)
    {
        return _list.AsQueryable().Any(predicate);
    }

    public T Create(T entity)
    {
        _list.Add(entity);
        return entity;
    }

    public bool Delete(Guid id)
    {
        var entityToDelete = GetById(id);
        return entityToDelete is not null && _list.Remove(entityToDelete);
    }

    public bool Delete(Expression<Func<T, bool>> predicate)
    {
        var toRemove = _list.AsQueryable().Where(predicate);
        foreach (var item in toRemove)
        {
            _list.Remove(item);
        }
        return toRemove.Any();
    }

    public bool Update(T entity)
    {
        var currentItem = GetById(entity.Id);
        if (currentItem == null)
            return false;
        int currentIndex = _list.IndexOf(currentItem);
        _list[currentIndex] = entity;
        return true;
    }
}
