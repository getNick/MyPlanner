using System.Linq.Expressions;
using MyPlanner.Data.Entities;

namespace MyPlanner.Data.Repositories;

public interface IRepository<T> where T : EntityBase
{
    /// <summary>
    ///   Get the total objects count.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///   Gets all objects from database
    /// </summary>
    IQueryable<T> Get();

    /// <summary>
    ///   Gets object by primary key.
    /// </summary>
    /// <param name="id"> primary key </param>
    /// <returns> </returns>
    T? GetById(Guid id);

    /// <summary>
    ///   Gets objects via optional filter, sort order, and includes
    /// </summary>
    /// <param name="filter"> </param>
    /// <param name="orderBy"> </param>
    /// <param name="includeProperties"> </param>
    /// <returns> </returns>
    IQueryable<T> Get(Expression<Func<T, bool>> filter = null, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null, string includeProperties = "");

    /// <summary>
    ///   Gets the object(s) is exists in database by specified filter.
    /// </summary>
    /// <param name="predicate"> Specified the filter expression </param>
    bool Contains(Expression<Func<T, bool>> predicate);

    /// <summary>
    ///   Find object by specified expression.
    /// </summary>
    /// <param name="predicate"> </param>
    T Find(Expression<Func<T, bool>> predicate);

    /// <summary>
    ///   Create a new object to database.
    /// </summary>
    /// <param name="entity"> Specified a new object to create. </param>
    T Create(T entity);

    /// <summary>
    ///   Deletes the object by primary key
    /// </summary>
    /// <param name="id"> </param>
    bool Delete(Guid id);

    /// <summary>
    ///   Delete objects from database by specified filter expression.
    /// </summary>
    /// <param name="predicate"> </param>
    bool Delete(Expression<Func<T, bool>> predicate);

    /// <summary>
    ///   Update object changes and save to database.
    /// </summary>
    /// <param name="entity"> Specified the object to save. </param>
    bool Update(T entity);
}
